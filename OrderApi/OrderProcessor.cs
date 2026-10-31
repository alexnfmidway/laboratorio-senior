using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Polly.CircuitBreaker;

namespace OrderApi;

public sealed class OrderProcessor(
    IHttpClientFactory clientFactory,
    CircuitBreakerMonitor circuitBreaker,
    IMemoryCache completedOrders,
    ILogger<OrderProcessor> logger)
{
    private const string CacheKeyPrefix = "order-idempotency:";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, Lazy<Task<OrderProcessingResult>>> _inflight = new();

    public async Task<OrderProcessingResult> ProcessAsync(
        string idempotencyKey,
        bool simulatePaymentFailure,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}{idempotencyKey}";
        if (completedOrders.TryGetValue<CachedOrder>(cacheKey, out var cached) && cached is not null)
        {
            return cached.SimulatePaymentFailure == simulatePaymentFailure
                ? cached.Result
                : OrderProcessingResult.Conflict("A chave de idempotência já foi usada com outra operação.");
        }

        var operation = _inflight.GetOrAdd(
            idempotencyKey,
            _ => new Lazy<Task<OrderProcessingResult>>(
                () => ProcessCoreAsync(simulatePaymentFailure, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var result = await operation.Value;
            if (result.Success)
            {
                completedOrders.Set(
                    cacheKey,
                    new CachedOrder(simulatePaymentFailure, result),
                    IdempotencyWindow);
            }

            return result.SimulatePaymentFailure == simulatePaymentFailure
                ? result
                : OrderProcessingResult.Conflict("A chave de idempotência já foi usada com outra operação.");
        }
        finally
        {
            ((ICollection<KeyValuePair<string, Lazy<Task<OrderProcessingResult>>>>)_inflight)
                .Remove(new KeyValuePair<string, Lazy<Task<OrderProcessingResult>>>(idempotencyKey, operation));
        }
    }

    private async Task<OrderProcessingResult> ProcessCoreAsync(
        bool simulatePaymentFailure,
        CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        Guid reservationId;

        try
        {
            using var inventoryResponse = await clientFactory.CreateClient("InventoryClient")
                .PostAsync("/inventory/reserve", content: null, cancellationToken);

            if (!inventoryResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Inventory reservation failed with HTTP {StatusCode}.", (int)inventoryResponse.StatusCode);
                return OrderProcessingResult.Failure(
                    (int)HttpStatusCode.ServiceUnavailable,
                    "inventory_unavailable",
                    "Não foi possível reservar o estoque.",
                    simulatePaymentFailure);
            }

            var reservation = await inventoryResponse.Content.ReadFromJsonAsync<InventoryReservation>(
                cancellationToken: cancellationToken);

            if (reservation is null || reservation.ReservationId == Guid.Empty)
            {
                logger.LogError("Inventory API returned an invalid reservation response.");
                return OrderProcessingResult.Failure(
                    (int)HttpStatusCode.BadGateway,
                    "invalid_inventory_response",
                    "O serviço de estoque retornou uma resposta inválida.",
                    simulatePaymentFailure);
            }

            reservationId = reservation.ReservationId;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not reserve inventory for order {OrderId}.", orderId);
            return OrderProcessingResult.Failure(
                (int)HttpStatusCode.ServiceUnavailable,
                "inventory_unavailable",
                "O serviço de estoque está indisponível.",
                simulatePaymentFailure);
        }

        try
        {
            var paymentPath = simulatePaymentFailure
                ? "/payment/process?simulateFailure=true"
                : "/payment/process";
            using var paymentResponse = await clientFactory.CreateClient("PaymentClient")
                .PostAsync(paymentPath, content: null, cancellationToken);

            if (!paymentResponse.IsSuccessStatusCode)
            {
                if ((int)paymentResponse.StatusCode >= 500 ||
                    paymentResponse.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    circuitBreaker.RecordFailure();
                }

                var compensated = await ReleaseInventoryAsync(reservationId);
                return OrderProcessingResult.Failure(
                    (int)HttpStatusCode.BadGateway,
                    compensated ? "payment_failed" : "compensation_failed",
                    compensated
                        ? "O pagamento falhou e a reserva de estoque foi desfeita."
                        : "O pagamento falhou e não foi possível desfazer a reserva de estoque.",
                    simulatePaymentFailure);
            }

            circuitBreaker.RecordSuccess();
            return OrderProcessingResult.Succeeded(orderId, simulatePaymentFailure);
        }
        catch (BrokenCircuitException exception)
        {
            circuitBreaker.RecordBlocked();
            logger.LogInformation(exception, "Payment request was rejected by the open circuit breaker.");
            var compensated = await ReleaseInventoryAsync(reservationId);
            return OrderProcessingResult.Failure(
                (int)HttpStatusCode.ServiceUnavailable,
                compensated ? "circuit_open" : "compensation_failed",
                compensated
                    ? "O disjuntor está aberto; a reserva de estoque foi desfeita."
                    : "O disjuntor está aberto e não foi possível desfazer a reserva de estoque.",
                simulatePaymentFailure);
        }
        catch (OperationCanceledException)
        {
            await ReleaseInventoryAsync(reservationId);
            throw;
        }
        catch (Exception exception)
        {
            circuitBreaker.RecordFailure();
            logger.LogError(exception, "Payment processing failed for order {OrderId}.", orderId);
            var compensated = await ReleaseInventoryAsync(reservationId);
            return OrderProcessingResult.Failure(
                (int)HttpStatusCode.ServiceUnavailable,
                compensated ? "payment_unavailable" : "compensation_failed",
                compensated
                    ? "O serviço de pagamento está indisponível; a reserva de estoque foi desfeita."
                    : "O pagamento está indisponível e não foi possível desfazer a reserva de estoque.",
                simulatePaymentFailure);
        }
    }

    private async Task<bool> ReleaseInventoryAsync(Guid reservationId)
    {
        try
        {
            using var response = await clientFactory.CreateClient("InventoryClient")
                .PostAsync($"/inventory/release/{reservationId}", content: null, CancellationToken.None);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            logger.LogCritical(
                "Inventory compensation for reservation {ReservationId} failed with HTTP {StatusCode}.",
                reservationId,
                (int)response.StatusCode);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Inventory compensation failed for reservation {ReservationId}.", reservationId);
        }

        return false;
    }

    private sealed record CachedOrder(bool SimulatePaymentFailure, OrderProcessingResult Result);
}

public sealed record InventoryReservation(Guid ReservationId);

public sealed record OrderProcessingResult(
    bool Success,
    Guid? OrderId,
    string Message,
    int StatusCode,
    string? ErrorCode,
    bool SimulatePaymentFailure)
{
    public static OrderProcessingResult Succeeded(Guid orderId, bool simulatePaymentFailure) =>
        new(true, orderId, "Pedido processado com sucesso.", (int)HttpStatusCode.OK, null, simulatePaymentFailure);

    public static OrderProcessingResult Failure(
        int statusCode,
        string errorCode,
        string message,
        bool simulatePaymentFailure) =>
        new(false, null, message, statusCode, errorCode, simulatePaymentFailure);

    public static OrderProcessingResult Conflict(string message) =>
        new(false, null, message, (int)HttpStatusCode.Conflict, "idempotency_conflict", false);
}
