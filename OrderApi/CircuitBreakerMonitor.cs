using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;

namespace OrderApi;

public sealed class CircuitBreakerMonitor
{
    private readonly object _sync = new();
    private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _policy;
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private int _consecutiveFailures;
    private int _totalBlocked;
    private int _totalSuccess;
    private DateTimeOffset? _openedAt;

    public CircuitBreakerMonitor(IConfiguration configuration, ILogger<CircuitBreakerMonitor> logger)
    {
        _failureThreshold = Math.Max(1, configuration.GetValue("CircuitBreaker:FailureThreshold", 3));
        _breakDuration = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("CircuitBreaker:BreakDurationSeconds", 30)));

        _policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: _failureThreshold,
                durationOfBreak: _breakDuration,
                onBreak: (_, duration) =>
                {
                    lock (_sync)
                    {
                        _openedAt = DateTimeOffset.UtcNow;
                    }

                    logger.LogWarning("Payment circuit breaker opened for {BreakDuration}.", duration);
                },
                onReset: () =>
                {
                    lock (_sync)
                    {
                        _consecutiveFailures = 0;
                        _openedAt = null;
                    }

                    logger.LogInformation("Payment circuit breaker closed after a successful probe.");
                },
                onHalfOpen: () => logger.LogInformation("Payment circuit breaker is half-open."));
    }

    public IAsyncPolicy<HttpResponseMessage> Policy => _policy;

    public CircuitBreakerSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            var state = _policy.CircuitState switch
            {
                CircuitState.Open => "Open",
                CircuitState.HalfOpen => "HalfOpen",
                CircuitState.Isolated => "Open",
                _ => "Closed"
            };
            var remainingSeconds = state == "Open" && _openedAt is not null
                ? Math.Max(0, (int)Math.Ceiling((_openedAt.Value + _breakDuration - DateTimeOffset.UtcNow).TotalSeconds))
                : 0;

            return new CircuitBreakerSnapshot(
                state,
                _consecutiveFailures,
                _totalBlocked,
                _totalSuccess,
                _failureThreshold,
                (int)_breakDuration.TotalSeconds,
                remainingSeconds);
        }
    }

    public void RecordFailure()
    {
        lock (_sync)
        {
            _consecutiveFailures++;
        }
    }

    public void RecordSuccess()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _totalSuccess++;
        }
    }

    public void RecordBlocked()
    {
        lock (_sync)
        {
            _totalBlocked++;
        }
    }

    public void Reset()
    {
        _policy.Reset();

        lock (_sync)
        {
            _consecutiveFailures = 0;
            _totalBlocked = 0;
            _totalSuccess = 0;
            _openedAt = null;
        }
    }
}

public sealed record CircuitBreakerSnapshot(
    string State,
    int ConsecutiveFailures,
    int TotalBlocked,
    int TotalSuccess,
    int FailureThreshold,
    int BreakDurationSeconds,
    int RemainingSeconds);
