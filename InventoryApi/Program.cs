using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;

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
var reservations = new ConcurrentDictionary<Guid, byte>();

app.MapGet("/health", () => Results.Ok(new { service = "InventoryApi", status = "Healthy" }));

app.MapPost("/inventory/reserve", async (ILogger<Program> logger) =>
{
    logger.LogInformation("Reserving inventory...");
    await Task.Delay(100);
    var reservationId = Guid.NewGuid();
    reservations.TryAdd(reservationId, 0);
    return Results.Ok(new { reservationId, message = "Inventory reserved" });
});

app.MapPost("/inventory/release/{reservationId:guid}", (Guid reservationId, ILogger<Program> logger) =>
{
    if (reservations.TryRemove(reservationId, out _))
    {
        logger.LogInformation("Released inventory reservation {ReservationId}.", reservationId);
        return Results.NoContent();
    }

    return Results.NotFound(new { message = "Inventory reservation not found" });
});

app.Run();
