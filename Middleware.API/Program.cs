using Middleware_Core.Configuration;
using Middleware_Core.Outbox;
using Middleware_Core.Protocols;
using Middleware_Core.Queue;
using Middleware_Core.Services;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

var options = new MiddlewareOptions();
builder.Configuration.GetSection("Middleware").Bind(options);

builder.Services.AddSingleton(options);

var contentRoot = builder.Environment.ContentRootPath;
X509Certificate2Collection? lisTrustRoots = TryLoadTrustRoots(options, contentRoot);
X509Certificate2? lisClientCertificate = TryLoadClientCertificate(options, contentRoot);

builder.Services
    .AddHttpClient<LisSenderService>()
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler
        {
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        };

        if (lisClientCertificate != null)
        {
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ClientCertificates.Add(lisClientCertificate);
            StructuredLog.Info("lis.security.client_certificate.enabled",
                new { subject = lisClientCertificate.Subject });
        }

        if (options.LisSecurity.AllowInvalidServerCertificate)
        {
            StructuredLog.Info("lis.security.allow_invalid_certificate", new { enabled = true });
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            return handler;
        }

        if (lisTrustRoots != null && lisTrustRoots.Count > 0)
        {
            StructuredLog.Info("lis.security.custom_trust_roots.enabled", new { count = lisTrustRoots.Count });
            handler.ServerCertificateCustomValidationCallback =
                (req, cert, chain, errors) =>
                    ValidateServerCertificateWithCustomRoots(
                        req,
                        cert,
                        errors,
                        lisTrustRoots,
                        options.LisSecurity.AllowServerCertificateNameMismatch);
        }

        return handler;
    });
builder.Services.AddSingleton<IncomingMessageQueue>();
builder.Services.AddSingleton<IOutboxRepository>(_ => new SqliteOutboxRepository(options.OutboxDbPath));
builder.Services.AddSingleton<OperationalMetrics>();
builder.Services.AddSingleton<IncomingMessageIngestionService>();
builder.Services.AddSingleton<OutboxDeliveryService>();

var app = builder.Build();

var queue = app.Services.GetRequiredService<IncomingMessageQueue>();
var outboxRepository = app.Services.GetRequiredService<IOutboxRepository>();
var ingestion = app.Services.GetRequiredService<IncomingMessageIngestionService>();
var delivery = app.Services.GetRequiredService<OutboxDeliveryService>();
var metrics = app.Services.GetRequiredService<OperationalMetrics>();

await outboxRepository.InitializeAsync();

var tcpServers = new List<TcpProtocolServer>();
var serialReceivers = new List<SerialProtocolReceiver>();
var fileMonitors = new List<FileMonitoring>();

void Enqueue(string raw, AnalyzerProfile profile)
{
    var lisAnalyzerName = string.IsNullOrWhiteSpace(profile.LisAnalyzerName)
        ? profile.Name
        : profile.LisAnalyzerName;
    queue.EnqueueAsync(new IncomingMessage(profile.SourceMachine, raw, lisAnalyzerName, DateTime.UtcNow)).AsTask().GetAwaiter().GetResult();
}

if (options.Analyzers.Count == 0)
{
    var fallback = new AnalyzerProfile
    {
        Name = "fallback-tcp",
        SourceMachine = "LegacyTcp",
        Transport = TransportType.Tcp,
        Protocol = ProtocolType.Raw,
        TcpPort = options.Tcp.Port
    };

    var server = new TcpProtocolServer(fallback, Enqueue);
    server.Start();
    tcpServers.Add(server);
    var fileMonitor = new FileMonitoring(options.WatchFolder, queue, "LegacyFile");
    fileMonitor.Start();
    fileMonitors.Add(fileMonitor);
}
else
{
    foreach (var analyzer in options.Analyzers)
    {
        switch (analyzer.Transport)
        {
            case TransportType.Tcp:
                var tcpServer = new TcpProtocolServer(analyzer, Enqueue);
                tcpServer.Start();
                tcpServers.Add(tcpServer);
                break;
            case TransportType.Serial:
                var serial = new SerialProtocolReceiver(analyzer, Enqueue);
                serial.Start();
                serialReceivers.Add(serial);
                break;
            case TransportType.File:
                var monitor = new FileMonitoring(analyzer, queue);
                monitor.Start();
                fileMonitors.Add(monitor);
                break;
        }
    }
}
var cts = new CancellationTokenSource();
app.Lifetime.ApplicationStopping.Register(() =>
{
    cts.Cancel();

    foreach (var server in tcpServers)
        server.Stop();

    foreach (var serial in serialReceivers)
        serial.Stop();
});

_ = Task.Run(() => ingestion.RunAsync(cts.Token), cts.Token);
_ = Task.Run(() => delivery.RunAsync(cts.Token), cts.Token);

app.MapGet("/", () => "Middleware running...");

app.MapGet("/ops/metrics", () => Results.Ok(metrics.Snapshot()));

app.MapGet("/ops/outbox/{status}", async (string status, int? limit, IOutboxRepository repo) =>
{
    var rows = await repo.GetByStatusAsync(status, limit ?? 100);
    return Results.Ok(rows.Select(r => new { r.Id, r.Status, r.RetryCount, r.NextAttemptUtc, r.LastError, r.CreatedUtc }));
});

app.MapGet("/ops/outbox-counts", async (IOutboxRepository repo) => Results.Ok(await repo.GetStatusCountsAsync()));

app.MapPost("/ops/retry/{id}", async (string id, IOutboxRepository repo) =>
{
    var updated = await repo.RequeueAsync(id);
    return updated > 0 ? Results.Ok(new { updated }) : Results.NotFound(new { message = "id not found" });
});

app.MapPost("/ops/replay", async (DateTime fromUtc, DateTime toUtc, bool includeSent, IOutboxRepository repo) =>
{
    var updated = await repo.RequeueRangeAsync(fromUtc, toUtc, includeSent);
    return Results.Ok(new { updated, fromUtc, toUtc, includeSent });
});

app.Run();

static X509Certificate2Collection? TryLoadTrustRoots(MiddlewareOptions options, string contentRoot)
{
    var path = options.LisSecurity.TrustStorePath;
    if (string.IsNullOrWhiteSpace(path))
        return null;

    var fullPath = ResolvePath(path, contentRoot);
    if (!File.Exists(fullPath))
    {
        StructuredLog.Error("lis.security.truststore.missing", new { path = fullPath });
        return null;
    }

    try
    {
        var collection = new X509Certificate2Collection();
        collection.Import(fullPath, options.LisSecurity.TrustStorePassword, X509KeyStorageFlags.DefaultKeySet);

        // Keep public certs only.
        var roots = new X509Certificate2Collection();
        foreach (var cert in collection)
        {
            try
            {
                roots.Add(new X509Certificate2(cert.Export(X509ContentType.Cert)));
            }
            catch
            {
                // Ignore malformed entries.
            }
        }

        return roots.Count == 0 ? null : roots;
    }
    catch (Exception ex)
    {
        StructuredLog.Error("lis.security.truststore.load_failed", new { path = fullPath, error = ex.Message });
        return null;
    }
}

static X509Certificate2? TryLoadClientCertificate(MiddlewareOptions options, string contentRoot)
{
    var certPath = options.LisSecurity.ClientCertificatePath;
    if (string.IsNullOrWhiteSpace(certPath))
        return null;

    var fullCertPath = ResolvePath(certPath, contentRoot);
    if (!File.Exists(fullCertPath))
    {
        StructuredLog.Error("lis.security.client_certificate.missing", new { path = fullCertPath });
        return null;
    }

    try
    {
        var ext = Path.GetExtension(fullCertPath);
        if (ext.Equals(".p12", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".pfx", StringComparison.OrdinalIgnoreCase))
        {
            // If needed, add a password option later. For now, assume no password.
            return new X509Certificate2(fullCertPath);
        }

        var keyPath = options.LisSecurity.ClientKeyPath;
        X509Certificate2 pemCert;
        if (!string.IsNullOrWhiteSpace(keyPath))
        {
            var fullKeyPath = ResolvePath(keyPath, contentRoot);
            if (!File.Exists(fullKeyPath))
            {
                StructuredLog.Error("lis.security.client_key.missing", new { path = fullKeyPath });
                return null;
            }

            pemCert = X509Certificate2.CreateFromPemFile(fullCertPath, fullKeyPath);
        }
        else
        {
            pemCert = X509Certificate2.CreateFromPemFile(fullCertPath);
        }

        // Rehydrate as PFX to avoid platform-specific issues with ephemeral keys.
        return new X509Certificate2(pemCert.Export(X509ContentType.Pfx));
    }
    catch (Exception ex)
    {
        StructuredLog.Error("lis.security.client_certificate.load_failed", new { path = fullCertPath, error = ex.Message });
        return null;
    }
}

static bool ValidateServerCertificateWithCustomRoots(
    HttpRequestMessage request,
    X509Certificate2? certificate,
    SslPolicyErrors errors,
    X509Certificate2Collection trustRoots,
    bool allowNameMismatch)
{
    if (certificate == null)
        return false;

    if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
        return false;

    if (!allowNameMismatch &&
        (errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
        return false;

    // If there are no chain errors, accept.
    if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) == 0)
        return true;

    using var chain = new X509Chain();
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;

    foreach (var root in trustRoots)
        chain.ChainPolicy.CustomTrustStore.Add(root);

    return chain.Build(certificate);
}

static string ResolvePath(string path, string contentRoot)
{
    return Path.IsPathRooted(path)
        ? path
        : Path.GetFullPath(path, contentRoot);
}
