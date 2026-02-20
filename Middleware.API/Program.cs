using Middleware_Core.Configuration;
using Middleware_Core.Outbox;
using Middleware_Core.Protocols;
using Middleware_Core.Queue;
using Middleware_Core.Services;

var builder = WebApplication.CreateBuilder(args);

var options = new MiddlewareOptions();
builder.Configuration.GetSection("Middleware").Bind(options);

builder.Services.AddSingleton(options);
builder.Services
    .AddHttpClient<LisSenderService>()
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        if (!options.LisSecurity.AllowInvalidServerCertificate)
            return new HttpClientHandler();

        StructuredLog.Info("lis.security.allow_invalid_certificate", new { enabled = true });
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
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
    queue.EnqueueAsync(new IncomingMessage(profile.SourceMachine, raw, profile.Name, DateTime.UtcNow)).AsTask().GetAwaiter().GetResult();
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
