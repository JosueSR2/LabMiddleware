using Middleware_Core.Builders;
using Middleware_Core.Parsers;
using Middleware_Core.Configuration;
using Middleware_Core.Outbox;
using Middleware_Core.Queue;
using Middleware_Core.Receivers;
using Middleware_Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Registrar servicios
builder.Services.AddHttpClient();
builder.Services.AddSingleton<LisSenderService>();
var options = new MiddlewareOptions();
builder.Configuration.GetSection("Middleware").Bind(options);

var app = builder.Build();

// Configuración
string watchFolder = "/home/linkdicom/Proyectos/LabMiddleware/TestingResources";
string lisUrl = "http://localhost:8080/OpenELIS-Global/middleware/receive-result";
builder.Services.AddSingleton(options);
builder.Services.AddHttpClient<LisSenderService>();
builder.Services.AddSingleton<IncomingMessageQueue>();
builder.Services.AddSingleton<IOutboxRepository>(_ => new SqliteOutboxRepository(options.OutboxDbPath));
builder.Services.AddSingleton<IncomingMessageIngestionService>();
builder.Services.AddSingleton<OutboxDeliveryService>();

var app = builder.Build();

// Obtener servicios
var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
var httpClient = httpClientFactory.CreateClient();

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

var fileMonitor = new FileMonitoring(options.WatchFolder, queue);
fileMonitor.Start();

// TCP Receiver
var tcpReceiver = new TcpReceiver(5001, rawMessage =>
if (options.Tcp.Enabled)
{
    ProcessIncomingMessage(rawMessage);
});
    var tcpReceiver = new Middleware_Core.Services.TcpReceiver(options.Tcp.Port, rawMessage =>
    {
        queue.EnqueueAsync(new IncomingMessage("Tcp", rawMessage, $"tcp:{options.Tcp.Port}", DateTime.UtcNow)).AsTask().GetAwaiter().GetResult();
    });
    tcpReceiver.Start();
}

void ProcessIncomingMessage(string rawMessage)
if (options.Serial.Enabled)
{
    try
    var serialReceiver = new SerialReceiver(options.Serial.PortName, options.Serial.BaudRate, rawMessage =>
    {
        var parser = ParserFactory.GetParser(string.Empty, rawMessage);
        var results = parser.Parse(rawMessage);

        foreach (var result in results)
        {
            Console.WriteLine($"Processed: {result.SampleId} - {result.TestCode} = {result.Value}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] {ex.Message}");
    }
        queue.EnqueueAsync(new IncomingMessage("Serial", rawMessage, options.Serial.PortName, DateTime.UtcNow)).AsTask().GetAwaiter().GetResult();
    });
    serialReceiver.Start();
}

tcpReceiver.Start();
var cts = new CancellationTokenSource();
app.Lifetime.ApplicationStopping.Register(() => cts.Cancel());

_ = Task.Run(() => ingestion.RunAsync(cts.Token), cts.Token);
_ = Task.Run(() => delivery.RunAsync(cts.Token), cts.Token);

app.MapGet("/", () => "Middleware running...");

app.Run();