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



app.MapPost("/payment/process", async (ILogger<Program> logger) =>
{
    logger.LogInformation("Processing payment...");
    // Simulate some work
    await Task.Delay(150);
    return Results.Ok(new { message = "Payment processed" });
});

app.Run();
