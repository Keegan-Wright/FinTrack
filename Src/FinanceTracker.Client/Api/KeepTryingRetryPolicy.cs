using Microsoft.AspNetCore.SignalR.Client;

namespace FinanceTracker.Client.Api;

/// <summary>
/// How long the sync hub connection waits before trying again straight away, then a little longer each time up to
/// <see cref="Longest"/>, and never giving up, so a server that's down for a while, e.g. being updated, is picked up
/// again once it's back. SignalR's default gives up after four tries.
/// </summary>
public sealed class KeepTryingRetryPolicy : IRetryPolicy
{
    /// <summary>
    /// The wait between tries once the first few have failed.
    /// </summary>
    public static readonly TimeSpan Longest = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan[] FirstWaits = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)];

    public TimeSpan? NextRetryDelay(RetryContext retryContext) => WaitBefore(retryContext.PreviousRetryCount);

    /// <summary>
    /// The wait before trying again after <paramref name="failedTries"/> tries have failed, counting from none.
    /// </summary>
    public static TimeSpan WaitBefore(long failedTries)
    {
        if (failedTries < FirstWaits.Length)
        {
            return FirstWaits[failedTries];
        }

        return Longest;
    }
}
