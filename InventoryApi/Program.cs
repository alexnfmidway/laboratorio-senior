using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("InventoryApi")
            .ConfigureResource(resource => resource.AddService("InventoryApi"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    });

var app = builder.Build();



app.MapPost("/inventory/reserve", async (ILogger<Program> logger) =>
{
    logger.LogInformation("Reserving inventory...");
    // Simulate some work
    await Task.Delay(100);
    return Results.Ok(new { message = "Inventory reserved" });
});

app.Run();
