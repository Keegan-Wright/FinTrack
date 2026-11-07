using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Notifications;

/// <summary>
/// Something FinTrack tells the user about, shown under the app bar's bell and emailed if SMTP setup, see
/// <see cref="NotificationSettings"/>. Raised once: <see cref="Key"/> says which event it is, so the same one isn't raised
/// again on the next run of whatever noticed it.
/// </summary>
public sealed class Notification : AggregateRoot
{
    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private Notification()
    {
    }
#pragma warning restore CS8618

    /// <param name="key">Which event this is, e.g. a connection and the date its consent ends.</param>
    /// <param name="link">Where in the app to deal with it, relative to the app, null for nowhere.</param>
    /// <exception cref="ArgumentException">The key, title or message is blank.</exception>
    public Notification(Guid userId, NotificationKind kind, string key, string title, string message, string? link)
        : base(userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Kind = kind;
        Key = key;
        Reword(title, message);
        Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
    }

    public NotificationKind Kind { get; private set; }

    /// <summary>
    /// Which event this is, unique for the user, so the same event raises one notification.
    /// </summary>
    public string Key { get; private set; }

    public string Title { get; private set; }

    public string Message { get; private set; }

    /// <summary>
    /// Where in the app to deal with it, relative to the app, null for nowhere.
    /// </summary>
    public string? Link { get; private set; }

    /// <summary>
    /// When the user read it, null while it's unread.
    /// </summary>
    public DateTime? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    /// <summary>
    /// Marks it read, keeping when it was first read.
    /// </summary>
    public void MarkRead(DateTime now) => ReadAt ??= now;

    [MemberNotNull(nameof(Title), nameof(Message))]
    private void Reword(string title, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Title = title.Trim();
        Message = message.Trim();
    }
}
