using Middleware_Core.Builders;
using Middleware_Core.Parsers;
using Middleware_Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Registrar servicios
builder.Services.AddHttpClient();
builder.Services.AddSingleton<LisSenderService>();

var app = builder.Build();

// Configuración
string watchFolder = "/home/linkdicom/Proyectos/LabMiddleware/TestingResources";
string lisUrl = "http://localhost:5284/api/Analyzer/receive-result";

// Obtener servicios
var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
var httpClient = httpClientFactory.CreateClient();

var lisSender = app.Services.GetRequiredService<LisSenderService>();

// Iniciar monitoreo
var fileMonitor = new FileMonitoring(
    watchFolder,
    lisSender,
    lisUrl
);

fileMonitor.Start();

// TCP Receiver
var tcpReceiver = new TcpReceiver(5001, rawMessage =>
{
    ProcessIncomingMessage(rawMessage);
});

void ProcessIncomingMessage(string rawMessage)
{
    try
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
}

tcpReceiver.Start();

app.MapGet("/", () => "Middleware running...");

app.Run();
