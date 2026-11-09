using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Notifications;

/// <summary>
/// Where a user's notifications go: under the bell, by email, and to their webhook when they've set one. A kind
/// turned off everywhere isn't raised at all. Also when some kinds are raised, e.g. how close to a budget's limit to
/// warn. One per user, a user without one has every kind shown and emailed and no webhook, so warnings such as a
/// bank's consent ending reach them without setting anything up.
/// </summary>
/// <remarks>
/// It keeps the kinds turned off rather than on, so a kind added later reaches everyone until they turn it off.
/// </remarks>
public sealed class NotificationSettings : AggregateRoot
{
    /// <summary>
    /// How far through a budget to warn, as a percentage of what it has, unless the user sets another.
    /// </summary>
    public const int DefaultBudgetWarningPercent = 80;

    /// <summary>
    /// How many days before a bill is due to remind them, unless the user sets another.
    /// </summary>
    public const int DefaultBillReminderDays = 3;

    /// <summary>
    /// The most days before a bill they can be reminded, a month.
    /// </summary>
    public const int MostBillReminderDays = 31;

    private readonly List<NotificationKind> _notInApp = [];
    private readonly List<NotificationKind> _notEmailed = [];
    private readonly List<NotificationKind> _notWebhooked = [];
    private readonly List<LowBalanceAlert> _lowBalanceAlerts = [];

    [Obsolete("EF Use Only")]
    private NotificationSettings()
    {
    }

    /// <summary>
    /// Settings as a user starts with them, every kind shown and emailed, and every kind sent to a webhook once they
    /// set one. Large transactions and low balances aren't watched until they set an amount.
    /// </summary>
    public NotificationSettings(Guid userId) : base(userId)
    {
        BudgetWarningPercent = DefaultBudgetWarningPercent;
        BillReminderDays = DefaultBillReminderDays;
    }

    /// <summary>
    /// The kinds shown under the bell and on the Notifications page.
    /// </summary>
    public IReadOnlyList<NotificationKind> InApp => AllBut(_notInApp);

    /// <summary>
    /// The kinds the user turned off showing in the app.
    /// </summary>
    public IReadOnlyList<NotificationKind> NotInApp => _notInApp.AsReadOnly();

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

    /// <summary>
    /// How far through a budget's money this month to warn them, as a percentage, 1 to 100. Going over is always
    /// said.
    /// </summary>
    public int BudgetWarningPercent { get; private set; }

    /// <summary>
    /// How many days before a bill is due to remind them, 1 to <see cref="MostBillReminderDays"/>.
    /// </summary>
    public int BillReminderDays { get; private set; }

    /// <summary>
    /// Tell them about money going out over this, null when they don't want to hear about large transactions.
    /// </summary>
    public decimal? LargeTransactionOver { get; private set; }

    /// <summary>
    /// The accounts they're told about when running low, one each.
    /// </summary>
    public IReadOnlyList<LowBalanceAlert> LowBalanceAlerts => _lowBalanceAlerts.AsReadOnly();

    public bool HasWebhook => WebhookUrl is not null;

    public bool IsInApp(NotificationKind kind) => !_notInApp.Contains(kind);

    public bool IsEmailed(NotificationKind kind) => !_notEmailed.Contains(kind);

    /// <summary>
    /// Whether <paramref name="kind"/> is posted, which needs a webhook.
    /// </summary>
    public bool IsWebhooked(NotificationKind kind) => HasWebhook && !_notWebhooked.Contains(kind);

    /// <summary>
    /// Shows <paramref name="kinds"/> in the app from now on and no others.
    /// </summary>
    public void ShowInAppOnly(IEnumerable<NotificationKind> kinds) => TurnOffAllBut(_notInApp, kinds);

    /// <summary>
    /// Emails <paramref name="kinds"/> from now on and no others.
    /// </summary>
    public void EmailOnly(IEnumerable<NotificationKind> kinds) => TurnOffAllBut(_notEmailed, kinds);

    /// <summary>
    /// Posts <paramref name="kinds"/> to their webhook from now on and no others.
    /// </summary>
    public void WebhookOnly(IEnumerable<NotificationKind> kinds) => TurnOffAllBut(_notWebhooked, kinds);

    /// <summary>
    /// Changes when budgets, bills and large transactions are said.
    /// </summary>
    /// <param name="budgetWarningPercent">See <see cref="BudgetWarningPercent"/>.</param>
    /// <param name="billReminderDays">See <see cref="BillReminderDays"/>.</param>
    /// <param name="largeTransactionOver">See <see cref="LargeTransactionOver"/>, null to stop.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside what it allows.</exception>
    public void ChangeAlerts(int budgetWarningPercent, int billReminderDays, decimal? largeTransactionOver)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(budgetWarningPercent, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(budgetWarningPercent, 100);
        ArgumentOutOfRangeException.ThrowIfLessThan(billReminderDays, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(billReminderDays, MostBillReminderDays);
        if (largeTransactionOver is { } over)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(over);
        }

        BudgetWarningPercent = budgetWarningPercent;
        BillReminderDays = billReminderDays;
        LargeTransactionOver = largeTransactionOver;
    }

    /// <summary>
    /// Watches the accounts in <paramref name="belowByAccount"/> from now on and no others. An account whose amount
    /// is unchanged stays as it was, one that changed is checked afresh, so they're told if it's already below.
    /// </summary>
    /// <param name="belowByAccount">How low each account can go before they're told.</param>
    public void WatchBalances(IReadOnlyDictionary<Guid, decimal> belowByAccount)
    {
        ArgumentNullException.ThrowIfNull(belowByAccount);

        List<LowBalanceAlert> alerts = [];
        foreach ((Guid accountId, decimal below) in belowByAccount)
        {
            LowBalanceAlert? existing = _lowBalanceAlerts.Find(x => x.AccountId == accountId);
            if (existing is not null && existing.Below == below)
            {
                alerts.Add(existing);
            }
            else
            {
                alerts.Add(new LowBalanceAlert(accountId, below));
            }
        }

        _lowBalanceAlerts.Clear();
        _lowBalanceAlerts.AddRange(alerts);
    }

    /// <summary>
    /// Records <paramref name="accountId"/>'s available balance, when they watch it.
    /// </summary>
    /// <returns>Whether it has just gone below what they set, which is when to tell them. Staying below says nothing
    /// more until it's been back above.</returns>
    public bool RecordBalance(Guid accountId, decimal available, DateTime now)
    {
        int index = _lowBalanceAlerts.FindIndex(x => x.AccountId == accountId);
        if (index < 0)
        {
            return false;
        }

        LowBalanceAlert alert = _lowBalanceAlerts[index];
        if (available >= alert.Below)
        {
            // Back above, so going below again is told
            if (alert.LowSince is not null)
            {
                _lowBalanceAlerts[index] = alert with { LowSince = null };
            }

            return false;
        }

        if (alert.LowSince is not null)
        {
            return false;
        }

        _lowBalanceAlerts[index] = alert with { LowSince = now };
        return true;
    }

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
    /// Whether <paramref name="kind"/> shows in the app for a user with <paramref name="settings"/>, null when they've
    /// never changed them.
    /// </summary>
    public static bool IsInAppFor(NotificationSettings? settings, NotificationKind kind) =>
        settings is null || settings.IsInApp(kind);

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
