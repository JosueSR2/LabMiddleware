using Middleware_Core.Builders;
using Middleware_Core.Parsers;
using Middleware_Core.Configuration;
using Middleware_Core.Outbox;
using Middleware_Core.Protocols;
using Middleware_Core.Queue;
using Middleware_Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Registrar servicios
builder.Services.AddHttpClient();
builder.Services.AddSingleton<LisSenderService>();

var app = builder.Build();

// Configuración
string watchFolder = "/home/linkdicom/Proyectos/LabMiddleware/TestingResources";
string lisUrl = "http://localhost:8080/OpenELIS-Global/middleware/receive-result";
var options = new MiddlewareOptions();
builder.Configuration.GetSection("Middleware").Bind(options);

builder.Services.AddSingleton(options);
builder.Services.AddHttpClient<LisSenderService>();
builder.Services.AddSingleton<IncomingMessageQueue>();
builder.Services.AddSingleton<IOutboxRepository>(_ => new SqliteOutboxRepository(options.OutboxDbPath));
builder.Services.AddSingleton<IncomingMessageIngestionService>();
builder.Services.AddSingleton<OutboxDeliveryService>();

// Obtener servicios
var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
var httpClient = httpClientFactory.CreateClient();
var app = builder.Build();

var lisSender = app.Services.GetRequiredService<LisSenderService>();
var queue = app.Services.GetRequiredService<IncomingMessageQueue>();
var outboxRepository = app.Services.GetRequiredService<IOutboxRepository>();
var ingestion = app.Services.GetRequiredService<IncomingMessageIngestionService>();
var delivery = app.Services.GetRequiredService<OutboxDeliveryService>();

// Iniciar monitoreo
var fileMonitor = new FileMonitoring(
    watchFolder,
    lisSender,
    lisUrl
);
await outboxRepository.InitializeAsync();

fileMonitor.Start();
var tcpServers = new List<TcpProtocolServer>();
var serialReceivers = new List<SerialProtocolReceiver>();
var fileMonitors = new List<FileMonitoring>();

// TCP Receiver
var tcpReceiver = new TcpReceiver(5001, rawMessage =>
void Enqueue(string raw, AnalyzerProfile profile)
{
    ProcessIncomingMessage(rawMessage);
});
queue.EnqueueAsync(new IncomingMessage(profile.SourceMachine, raw, profile.Name, DateTime.UtcNow)).AsTask().GetAwaiter().GetResult();
}

void ProcessIncomingMessage(string rawMessage)
if (options.Analyzers.Count == 0)
{
    try
    var fallback = new AnalyzerProfile
    {
        var parser = ParserFactory.GetParser(string.Empty, rawMessage);
        var results = parser.Parse(rawMessage);
        Name = "fallback-tcp",
        SourceMachine = "LegacyTcp",
        Transport = TransportType.Tcp,
        Protocol = ProtocolType.Raw,
        TcpPort = options.Tcp.Port
    };

    var server = new TcpProtocolServer(fallback, Enqueue);
    server.Start();
    tcpServers.Add(server);

    foreach (var result in results)
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
            Console.WriteLine($"Processed: {result.SampleId} - {result.TestCode} = {result.Value}");
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
    catch (Exception ex)
    {
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
    }

    tcpReceiver.Start();
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

    app.Run();