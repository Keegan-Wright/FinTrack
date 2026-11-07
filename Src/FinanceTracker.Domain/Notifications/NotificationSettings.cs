using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Notifications;

/// <summary>
/// Which notifications a user also has emailed, every one shows under the bell whatever this says. One per user, a
/// user without one has every kind emailed, so warnings such as a bank's consent ending reach them without setting
/// anything up.
/// </summary>
public sealed class NotificationSettings : AggregateRoot
{
    private readonly List<NotificationKind> _emailed = [];

    [Obsolete("EF Use Only")]
    private NotificationSettings()
    {
    }

    /// <summary>
    /// Settings as a user starts with them, every kind emailed.
    /// </summary>
    public NotificationSettings(Guid userId) : base(userId) => _emailed.AddRange(Enum.GetValues<NotificationKind>());

    /// <summary>
    /// The kinds the user has emailed.
    /// </summary>
    public IReadOnlyList<NotificationKind> Emailed => _emailed.AsReadOnly();

    public bool IsEmailed(NotificationKind kind) => _emailed.Contains(kind);

    /// <summary>
    /// Emails <paramref name="kinds"/> from now on and no others.
    /// </summary>
    public void EmailOnly(IEnumerable<NotificationKind> kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        _emailed.Clear();
        _emailed.AddRange(kinds.Distinct().Order());
    }

    /// <summary>
    /// Whether <paramref name="kind"/> is emailed for a user with <paramref name="settings"/>, null when they've never
    /// changed them.
    /// </summary>
    public static bool IsEmailedFor(NotificationSettings? settings, NotificationKind kind) =>
        settings is null || settings.IsEmailed(kind);
}
