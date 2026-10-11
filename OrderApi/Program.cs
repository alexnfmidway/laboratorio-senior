using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("OrderApi")
            .ConfigureResource(resource => resource.AddService("OrderApi"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    });

// Configure HttpClient with Polly Circuit Breaker
builder.Services.AddHttpClient("InventoryClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["InventoryApiUrl"] ?? "http://localhost:5001");
})
.AddTransientHttpErrorPolicy(policyBuilder => policyBuilder.CircuitBreakerAsync(
    handledEventsAllowedBeforeBreaking: 3,
    durationOfBreak: TimeSpan.FromSeconds(30)
));

builder.Services.AddHttpClient("PaymentClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PaymentApiUrl"] ?? "http://localhost:5002");
})
.AddTransientHttpErrorPolicy(policyBuilder => policyBuilder.CircuitBreakerAsync(
    handledEventsAllowedBeforeBreaking: 3,
    durationOfBreak: TimeSpan.FromSeconds(30)
));

var app = builder.Build();



app.MapPost("/order", async (IHttpClientFactory clientFactory, ILogger<Program> logger) =>
{
    logger.LogInformation("Processing order...");

    var inventoryClient = clientFactory.CreateClient("InventoryClient");
    var paymentClient = clientFactory.CreateClient("PaymentClient");

    try
    {
        // Call Inventory API
        logger.LogInformation("Calling Inventory API");
        var inventoryResponse = await inventoryClient.PostAsync("/inventory/reserve", null);
        inventoryResponse.EnsureSuccessStatusCode();

        // Call Payment API
        logger.LogInformation("Calling Payment API");
        var paymentResponse = await paymentClient.PostAsync("/payment/process", null);
        paymentResponse.EnsureSuccessStatusCode();

        return Results.Ok(new { message = "Order processed successfully", orderId = Guid.NewGuid() });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing order");
        return Results.StatusCode(500);
    }
});

app.Run();
