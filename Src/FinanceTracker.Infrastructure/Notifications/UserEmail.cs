namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// A user's email and whether they've confirmed it, see <see cref="NotificationsService"/>.
/// </summary>
/// <param name="Address">Their email, null when they have none.</param>
/// <param name="IsConfirmed">Whether they've confirmed it.</param>
internal sealed record UserEmail(string? Address, bool IsConfirmed);
