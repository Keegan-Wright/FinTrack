using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tokens;

/// <summary>
/// <see cref="ITokenFailureLimiter"/> over a fixed hour per address. Once an address reaches the limit, its tokens
/// aren't looked at until the hour is up, so guessing can't be done cheaply.
/// </summary>
public sealed class TokenFailureLimiter : ITokenFailureLimiter, IDisposable
{
    /// <summary>
    /// How long the count lasts, an hour.
    /// </summary>
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly PartitionedRateLimiter<string> _limiter;

    /// <param name="options">How many refusals an address may have in <see cref="Window"/>.</param>
    public TokenFailureLimiter(IOptions<TokenFailureLimiterOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        int failuresAllowed = options.Value.FailuresPerHour;
        ArgumentOutOfRangeException.ThrowIfLessThan(failuresAllowed, 1);
        _limiter = PartitionedRateLimiter.Create<string, string>(address =>
            RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = failuresAllowed,
                Window = Hour,
                QueueLimit = 0
            }));
    }

    /// <inheritdoc/>
    public TimeSpan Window => Hour;

    /// <inheritdoc/>
    public bool IsBlocked(string address) => _limiter.GetStatistics(address)?.CurrentAvailablePermits == 0;

    /// <inheritdoc/>
    public void RecordFailure(string address)
    {
        using RateLimitLease lease = _limiter.AttemptAcquire(address);
    }

    /// <inheritdoc/>
    public void Dispose() => _limiter.Dispose();
}
