using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// CORS para o Blazor Frontend (desenvolvimento e Docker)
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorPolicy", policy =>
        policy
            .WithOrigins(
                "http://localhost:3000",  // Blazor via Docker
                "http://localhost:5001",  // Blazor dev local
                "https://localhost:7001",
                "http://localhost:5241",
                "https://localhost:7241"
            )
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Adiciona HttpClient para health checks agregados
builder.Services.AddHttpClient("HealthCheckClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(3);
});

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

// Ativa CORS antes do proxy
app.UseCors("BlazorPolicy");

// Endpoint de diagnóstico do Gateway
app.MapGet("/gateway/health", () => Results.Ok(new 
{ 
    service = "YARP API Gateway", 
    status = "Healthy",
    role = "Single Entry Point",
    provider = "Microsoft Yarp.ReverseProxy (.NET 10)",
    upstream = "http://orderapi:8080"
}));

// Endpoints agregados de saúde dos microsserviços privados
string GetUrl(string key, string fallback) => app.Configuration[key] ?? fallback;

app.MapGet("/order-health", async (IHttpClientFactory clientFactory) =>
{
    try
    {
        var client = clientFactory.CreateClient("HealthCheckClient");
        var url = GetUrl("OrderApiUrl", "http://orderapi:8080");
        var res = await client.GetAsync($"{url}/health");
        return res.IsSuccessStatusCode ? Results.Ok(new { service = "OrderApi", status = "Healthy" }) : Results.StatusCode((int)res.StatusCode);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 503);
    }
});

app.MapGet("/inventory-health", async (IHttpClientFactory clientFactory) =>
{
    try
    {
        var client = clientFactory.CreateClient("HealthCheckClient");
        var url = GetUrl("InventoryApiUrl", "http://inventoryapi:8080");
        var res = await client.GetAsync($"{url}/health");
        return res.IsSuccessStatusCode ? Results.Ok(new { service = "InventoryApi", status = "Healthy" }) : Results.StatusCode((int)res.StatusCode);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 503);
    }
});

app.MapGet("/payment-health", async (IHttpClientFactory clientFactory) =>
{
    try
    {
        var client = clientFactory.CreateClient("HealthCheckClient");
        var url = GetUrl("PaymentApiUrl", "http://paymentapi:8080");
        var res = await client.GetAsync($"{url}/health");
        return res.IsSuccessStatusCode ? Results.Ok(new { service = "PaymentApi", status = "Healthy" }) : Results.StatusCode((int)res.StatusCode);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 503);
    }
});

// 3. Mapeia o pipeline de proxy reverso do YARP
app.MapReverseProxy();

app.Run();
