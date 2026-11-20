using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// Pushes a message to one of a user's browsers.
/// </summary>
public interface IPushSender
{
    /// <summary>
    /// Encrypts <paramref name="message"/> for <paramref name="device"/> and hands it to its push service. One try.
    /// </summary>
    /// <param name="device">The browser.</param>
    /// <param name="message">What it shows.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Whether it was taken, or the browser is gone.</returns>
    /// <exception cref="PushRejectedException">The push service answered, but not with success.</exception>
    /// <exception cref="HttpRequestException">It couldn't be reached, or its address is refused.</exception>
    /// <exception cref="TaskCanceledException">It didn't answer in time.</exception>
    Task<PushOutcome> SendAsync(PushDevice device, PushMessage message, CancellationToken cancellationToken);
}
