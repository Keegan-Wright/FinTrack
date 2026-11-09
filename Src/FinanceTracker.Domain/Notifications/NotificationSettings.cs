using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Notifications;

/// <summary>
/// Where a user's notifications go besides the bell, every one shows there whatever this says. By email, and to
/// their webhook when they've set one. One per user, a user without one has every kind emailed and no webhook, so
/// warnings such as a bank's consent ending reach them without setting anything up.
/// </summary>
/// <remarks>
/// It keeps the kinds turned off rather than on, so a kind added later reaches everyone until they turn it off.
/// </remarks>
public sealed class NotificationSettings : AggregateRoot
{
    private readonly List<NotificationKind> _notEmailed = [];
    private readonly List<NotificationKind> _notWebhooked = [];

    [Obsolete("EF Use Only")]
    private NotificationSettings()
    {
    }

    /// <summary>
    /// Settings as a user starts with them, every kind emailed, and every kind sent to a webhook once they set one.
    /// </summary>
    public NotificationSettings(Guid userId) : base(userId)
    {
    }

    /// <summary>
    /// The kinds the user has emailed.
    /// </summary>
    public IReadOnlyList<NotificationKind> Emailed => AllBut(_notEmailed);

    /// <summary>
    /// The kinds the user turned off emailing.
    /// </summary>
    public IReadOnlyList<NotificationKind> NotEmailed => _notEmailed.AsReadOnly();

    /// <summary>
    ///  Last tried email, a notification or a test, null when it never has.
    /// </summary>
    public DateTime? LastEmailAt { get; private set; }

    /// <summary>
    /// Why that email couldn't be sent, in words for the user, null when it was sent.
    /// </summary>
    public string? LastEmailFailure { get; private set; }

    /// <summary>
    /// Where their notifications are posted, null when they haven't set a webhook.
    /// </summary>
    public string? WebhookUrl { get; private set; }

    /// <summary>
    /// What each post to <see cref="WebhookUrl"/> is signed with, so whatever receives it can check it came from
    /// FinTrack. Null without a webhook.
    /// </summary>
    public string? WebhookSecret { get; private set; }

    /// <summary>
    /// The kinds posted to their webhook, when they have one.
    /// </summary>
    public IReadOnlyList<NotificationKind> Webhooked => AllBut(_notWebhooked);

    /// <summary>
    /// The kinds the user turned off posting to their webhook.
    /// </summary>
    public IReadOnlyList<NotificationKind> NotWebhooked => _notWebhooked.AsReadOnly();

    /// <summary>
    /// Last tried posting to their webhook, null when it never has.
    /// </summary>
    public DateTime? LastWebhookAt { get; private set; }

    /// <summary>
    /// Why that post failed, in words for the user, null when it was delivered.
    /// </summary>
    public string? LastWebhookFailure { get; private set; }

    public bool HasWebhook => WebhookUrl is not null;

    public bool IsEmailed(NotificationKind kind) => !_notEmailed.Contains(kind);

    /// <summary>
    /// Whether <paramref name="kind"/> is posted, which needs a webhook.
    /// </summary>
    public bool IsWebhooked(NotificationKind kind) => HasWebhook && !_notWebhooked.Contains(kind);

    /// <summary>
    /// Emails <paramref name="kinds"/> from now on and no others.
    /// </summary>
    public void EmailOnly(IEnumerable<NotificationKind> kinds) => TurnOffAllBut(_notEmailed, kinds);

    /// <summary>
    /// Posts <paramref name="kinds"/> to their webhook from now on and no others.
    /// </summary>
    public void WebhookOnly(IEnumerable<NotificationKind> kinds) => TurnOffAllBut(_notWebhooked, kinds);

    /// <summary>
    /// Records an email sent at <paramref name="at"/>, or that it couldn't be and why.
    /// </summary>
    /// <param name="failure">Why it couldn't be sent, null when it was.</param>
    public void RecordEmail(DateTime at, string? failure)
    {
        LastEmailAt = at;
        LastEmailFailure = Trimmed(failure);
    }

    /// <summary>
    /// Posts their notifications to <paramref name="url"/> from now on. A new webhook gets a new
    /// <paramref name="secret"/> and forgets how the last one went; changing the address keeps the secret, so what
    /// receives it keeps checking posts the same way.
    /// </summary>
    /// <param name="secret">The secret for a new webhook, unused when they already have one.</param>
    /// <exception cref="ArgumentException">The address or secret is blank.</exception>
    public void SetWebhook(string url, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        if (!HasWebhook)
        {
            WebhookSecret = secret;
            LastWebhookAt = null;
            LastWebhookFailure = null;
        }

        WebhookUrl = url.Trim();
    }

    /// <summary>
    /// Signs posts with <paramref name="secret"/> from now on, e.g. the old one leaked.
    /// </summary>
    /// <returns>Whether they have a webhook to sign for.</returns>
    public bool ReplaceWebhookSecret(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        if (!HasWebhook)
        {
            return false;
        }

        WebhookSecret = secret;
        return true;
    }

    /// <summary>
    /// Stops posting to their webhook, forgetting its address and secret. Which kinds it took are kept for the next.
    /// </summary>
    public void RemoveWebhook()
    {
        WebhookUrl = null;
        WebhookSecret = null;
        LastWebhookAt = null;
        LastWebhookFailure = null;
    }

    /// <summary>
    /// Records a post delivered at <paramref name="at"/>, or that it failed and why.
    /// </summary>
    /// <param name="failure">Why it failed, null when it was delivered.</param>
    public void RecordWebhook(DateTime at, string? failure)
    {
        LastWebhookAt = at;
        LastWebhookFailure = Trimmed(failure);
    }

    /// <summary>
    /// Whether <paramref name="kind"/> is emailed for a user with <paramref name="settings"/>, null when they've never
    /// changed them.
    /// </summary>
    public static bool IsEmailedFor(NotificationSettings? settings, NotificationKind kind) =>
        settings is null || settings.IsEmailed(kind);

    private static void TurnOffAllBut(List<NotificationKind> off, IEnumerable<NotificationKind> on)
    {
        ArgumentNullException.ThrowIfNull(on);
        HashSet<NotificationKind> wanted = on.ToHashSet();
        off.Clear();
        off.AddRange(Enum.GetValues<NotificationKind>().Where(x => !wanted.Contains(x)));
    }

    private static List<NotificationKind> AllBut(List<NotificationKind> off) =>
        Enum.GetValues<NotificationKind>().Where(x => !off.Contains(x)).ToList();

    private static string? Trimmed(string? failure) => string.IsNullOrWhiteSpace(failure) ? null : failure.Trim();
}
