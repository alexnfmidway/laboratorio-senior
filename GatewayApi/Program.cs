using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// 1. Configura OpenTelemetry com Tracing no YARP API Gateway
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("gatewayapi"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation() // Captura requisições de entrada no Gateway
            .AddHttpClientInstrumentation() // Captura o proxy reverso encaminhado para o OrderApi
            .AddOtlpExporter(opt => opt.Endpoint = new Uri("http://jaeger:4317"));
    });

// 2. Carrega as rotas e clusters do YARP a partir do appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Endpoint de diagnóstico do Gateway
app.MapGet("/gateway/health", () => Results.Ok(new 
{ 
    service = "YARP API Gateway", 
    status = "Healthy",
    role = "Single Entry Point",
    provider = "Microsoft Yarp.ReverseProxy (.NET 10)",
    upstream = "http://orderapi:8080"
}));

// 3. Mapeia o pipeline de proxy reverso do YARP
app.MapReverseProxy();

app.Run();
