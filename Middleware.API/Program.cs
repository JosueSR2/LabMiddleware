using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Middleware.Core.Receivers;
using Middleware.Core.Parsers;
using Middleware.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Registrar HttpClient para LIS
builder.Services.AddHttpClient<LisSenderService>();

var app = builder.Build();

// Configuración carpeta y URL LIS
string watchFolder = "/home/linkdicom/Proyectos/LabMiddleware/TestingResources";
string lisUrl = "http://localhost:5284/api/Analyzer/receive-result";

// Crear parser y processor
var parser = new DimensionParser();
var processor = new AnalyzerMessageProcessor(parser);

// Iniciar servicio de monitoreo
var fileMonitor = new FileMonitoringService(
    watchFolder,
    processor,
    app.Services.GetRequiredService<LisSenderService>(),
    lisUrl
);
fileMonitor.Start();

var tcpReceiver = new TcpReceiver(5001, rawMessage =>
{
    ProcessIncomingMessage(rawMessage);
});

void ProcessIncomingMessage(string rawMessage)
{
    try
    {
        var format = AnalyzerFormatDetector.Detect(rawMessage);
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

// Endpoint mínimo
app.MapGet("/", () => "Middleware running...");

app.Run();

