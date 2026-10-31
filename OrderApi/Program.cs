using OrderApi;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CircuitBreakerMonitor>();
builder.Services.AddScoped<OrderProcessor>();

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

builder.Services.AddHttpClient("InventoryClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["InventoryApiUrl"] ?? "http://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient("PaymentClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PaymentApiUrl"] ?? "http://localhost:5002");
    client.Timeout = TimeSpan.FromSeconds(5);
})
.AddPolicyHandler((sp, _) => sp.GetRequiredService<CircuitBreakerMonitor>().Policy);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "OrderApi", status = "Healthy" }));

app.MapGet("/order/circuit-breaker", (CircuitBreakerMonitor monitor) =>
    Results.Ok(monitor.GetSnapshot()));

app.MapPost("/order/circuit-breaker/reset", (CircuitBreakerMonitor monitor, IConfiguration configuration) =>
    configuration.GetValue<bool>("Demo:AllowCircuitBreakerReset")
        ? ResetCircuitBreaker(monitor)
        : Results.NotFound());

app.MapPost("/order", async (
    HttpContext context,
    OrderProcessor processor,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();
    if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
    {
        return Results.Problem(
            title: "invalid_idempotency_key",
            detail: "Envie um cabeçalho Idempotency-Key com até 128 caracteres.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var simulatePaymentFailure = context.Request.Query["simulatePaymentFailure"] == "true";
    if (simulatePaymentFailure && !configuration.GetValue<bool>("Demo:AllowFailureSimulation"))
    {
        return Results.NotFound();
    }

    var result = await processor.ProcessAsync(idempotencyKey, simulatePaymentFailure, cancellationToken);
    if (!result.Success)
    {
        return Results.Problem(
            title: result.ErrorCode,
            detail: result.Message,
            statusCode: result.StatusCode);
    }

    return Results.Ok(new { message = result.Message, orderId = result.OrderId });
});

app.Run();

static IResult ResetCircuitBreaker(CircuitBreakerMonitor monitor)
{
    monitor.Reset();
    return Results.Ok(monitor.GetSnapshot());
}
