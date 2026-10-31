using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("PaymentApi")
            .ConfigureResource(resource => resource.AddService("PaymentApi"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    });

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "PaymentApi", status = "Healthy" }));

app.MapPost("/payment/process", async (
    HttpContext context,
    IConfiguration configuration,
    ILogger<Program> logger) =>
{
    if (context.Request.Query["simulateFailure"] == "true")
    {
        if (!configuration.GetValue<bool>("Demo:AllowFailureSimulation"))
        {
            return Results.NotFound();
        }

        logger.LogWarning("Simulating a payment failure for the demo.");
        return Results.Problem(
            title: "simulated_payment_failure",
            detail: "Falha de pagamento simulada para demonstrar o Circuit Breaker.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    logger.LogInformation("Processing payment...");
    await Task.Delay(150);
    return Results.Ok(new { message = "Payment processed" });
});

app.Run();
