using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Raises notifications for the current user, wherever they want that kind: under the bell, pushed to their open
/// browsers, emailed when a mail server is set up, and posted to their webhook. A kind they've turned off everywhere
/// isn't raised.
/// </summary>
public interface INotifier
{
    /// <summary>
    /// Raises the notification unless one with the same <paramref name="key"/> already was, so whatever noticed the
    /// event can run again without saying it twice.
    /// </summary>
    /// <param name="key">Which event it is, e.g. a connection and the date its consent ends.</param>
    /// <param name="link">Where in the app to deal with it, relative to the app, null for nowhere.</param>
    /// <returns>Whether it was raised now, false when it already was or nowhere wants it.</returns>
    Task<bool> NotifyAsync(NotificationKind kind, string key, string title, string message, string? link,
        CancellationToken cancellationToken);
}
