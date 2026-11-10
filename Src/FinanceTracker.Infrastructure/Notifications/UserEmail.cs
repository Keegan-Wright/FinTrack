namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// A user's email and whether they've confirmed it, see <see cref="NotificationsService"/>.
/// </summary>
internal sealed record UserEmail(string? Address, bool IsConfirmed);
