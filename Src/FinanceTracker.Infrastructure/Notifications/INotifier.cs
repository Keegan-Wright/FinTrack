using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Raises notifications for the current user, wherever they want that kind. Under the bell, emailed when a mail server
/// is set up, posted to their webhook, and pushed to their devices. A kind they've turned off everywhere isn't raised.
/// </summary>
public interface INotifier
{
    /// <summary>
    /// Raises the notification unless one with the same <paramref name="key"/> already was, so whatever noticed the
    /// event can run again without saying it twice.
    /// </summary>
    /// <param name="kind">What it's about.</param>
    /// <param name="key">Which event it is, e.g. a connection and the date its consent ends.</param>
    /// <param name="title">Its heading.</param>
    /// <param name="message">What it says.</param>
    /// <param name="link">Where in the app to deal with it, relative to the app, null for nowhere.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Whether it was raised now, false when it already was or nowhere wants it.</returns>
    Task<bool> NotifyAsync(NotificationKind kind, string key, string title, string message, string? link,
        CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="NotifyAsync"/> for a notification that says amounts. A push to a locked phone shows
    /// <paramref name="titleWithoutAmounts"/> instead, and no message, unless the user wants amounts there.
    /// </summary>
    /// <param name="kind">What it's about.</param>
    /// <param name="key">Which event it is.</param>
    /// <param name="title">Its heading.</param>
    /// <param name="titleWithoutAmounts">Its heading without the money, e.g. <c>Groceries is nearly used up</c>.</param>
    /// <param name="message">What it says.</param>
    /// <param name="link">Where in the app to deal with it, relative to the app, null for nowhere.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Whether it was raised now, false when it already was or nowhere wants it.</returns>
    Task<bool> NotifyWithAmountsAsync(NotificationKind kind, string key, string title, string titleWithoutAmounts,
        string message, string? link, CancellationToken cancellationToken);
}
