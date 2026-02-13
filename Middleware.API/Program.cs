using Middleware.Core.Parsers;
using Middleware.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// HttpClient para enviar mensajes
builder.Services.AddHttpClient<LisSenderService>();

// Crear parser (para pruebas usamos Dimension)
var parser = new DimensionParser();
var processor = new AnalyzerMessageProcessor(parser);

var app = builder.Build();

// Configuración de carpeta y URL del LIS
string watchFolder = @"C:\Laboratory\TestingResources";
string lisUrl = "http://localhost:5284/api/Analyzer/receive-result";

// Iniciar servicio de monitoreo
var fileMonitor = new FileMonitoringService(watchFolder, processor, app.Services.GetRequiredService<LisSenderService>(), lisUrl);
fileMonitor.Start();

app.MapGet("/", () => "Middleware running...");

app.Run();
