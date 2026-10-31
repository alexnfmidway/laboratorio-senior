using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OrderApi;
using Polly.CircuitBreaker;
using Xunit;

namespace OrderApi.Tests;

public sealed class OrderProcessorTests
{
    [Fact]
    public async Task Repeated_successful_key_returns_same_order_without_repeating_calls()
    {
        var reservationId = Guid.NewGuid();
        var clientFactory = new FakeHttpClientFactory((name, request, _) =>
        {
            if (name == "InventoryClient" && request.RequestUri!.AbsolutePath == "/inventory/reserve")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { reservationId })
                });
            }

            if (name == "PaymentClient")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }

            throw new InvalidOperationException($"Unexpected request: {name} {request.RequestUri}");
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var processor = CreateProcessor(clientFactory, cache);

        var first = await processor.ProcessAsync("order-123", false, CancellationToken.None);
        var replay = await processor.ProcessAsync("order-123", false, CancellationToken.None);

        Assert.True(first.Success);
        Assert.Equal(first.OrderId, replay.OrderId);
        Assert.Equal(2, clientFactory.RequestCount);
    }

    [Fact]
    public async Task Payment_failure_releases_inventory_reservation()
    {
        var reservationId = Guid.NewGuid();
        var released = false;
        var clientFactory = new FakeHttpClientFactory((name, request, _) =>
        {
            if (name == "InventoryClient" && request.RequestUri!.AbsolutePath == "/inventory/reserve")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { reservationId })
                });
            }

            if (name == "PaymentClient")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            if (name == "InventoryClient" && request.RequestUri!.AbsolutePath == $"/inventory/release/{reservationId}")
            {
                released = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            throw new InvalidOperationException($"Unexpected request: {name} {request.RequestUri}");
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var processor = CreateProcessor(clientFactory, cache);

        var result = await processor.ProcessAsync("order-failed", true, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("payment_failed", result.ErrorCode);
        Assert.True(result.SimulatePaymentFailure);
        Assert.True(released);
    }

    [Fact]
    public async Task Same_key_with_different_operation_returns_conflict()
    {
        var reservationId = Guid.NewGuid();
        var clientFactory = new FakeHttpClientFactory((name, request, _) =>
        {
            if (name == "InventoryClient")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { reservationId })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var processor = CreateProcessor(clientFactory, cache);

        var first = await processor.ProcessAsync("order-conflict", false, CancellationToken.None);
        var conflict = await processor.ProcessAsync("order-conflict", true, CancellationToken.None);

        Assert.True(first.Success);
        Assert.Equal((int)HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("idempotency_conflict", conflict.ErrorCode);
        Assert.Equal(2, clientFactory.RequestCount);
    }

    [Fact]
    public async Task Circuit_breaker_opens_at_configured_failure_threshold()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CircuitBreaker:FailureThreshold"] = "2",
                ["CircuitBreaker:BreakDurationSeconds"] = "5"
            })
            .Build();
        var monitor = new CircuitBreakerMonitor(configuration, NullLogger<CircuitBreakerMonitor>.Instance);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await monitor.Policy.ExecuteAsync(
                () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        }

        Assert.Equal("Open", monitor.GetSnapshot().State);
        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(
            () => monitor.Policy.ExecuteAsync(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
    }

    private static OrderProcessor CreateProcessor(FakeHttpClientFactory clientFactory, IMemoryCache cache)
    {
        var monitor = new CircuitBreakerMonitor(
            new ConfigurationBuilder().Build(),
            NullLogger<CircuitBreakerMonitor>.Instance);
        return new OrderProcessor(
            clientFactory,
            monitor,
            cache,
            NullLogger<OrderProcessor>.Instance);
    }

    private sealed class FakeHttpClientFactory(
        Func<string, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : IHttpClientFactory
    {
        private int _requestCount;
        public int RequestCount => _requestCount;

        public HttpClient CreateClient(string name) =>
            new(new DelegateHandler((request, cancellationToken) =>
            {
                Interlocked.Increment(ref _requestCount);
                return sendAsync(name, request, cancellationToken);
            }))
            {
                BaseAddress = new Uri("http://localhost")
            };
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            sendAsync(request, cancellationToken);
    }
}
