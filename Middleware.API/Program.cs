using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Middleware.Core.Parsers;
using Middleware.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Registrar HttpClient para LIS
builder.Services.AddHttpClient<LisSenderService>();

var app = builder.Build();

// Configuración carpeta y URL LIS
string watchFolder = @"C:\Laboratory\TestingResources";
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

// Endpoint mínimo
app.MapGet("/", () => "Middleware running...");

app.Run();

