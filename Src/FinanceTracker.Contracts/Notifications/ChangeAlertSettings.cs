namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// When budgets, bills, large transactions and low balances are said, see
/// <see cref="Validation.ChangeAlertSettingsValidator"/>.
/// </summary>
public sealed class ChangeAlertSettings
{
    /// <summary>
    /// The most days before a bill they can be reminded, a month.
    /// </summary>
    public const int MostBillReminderDays = 31;

    /// <summary>
    /// How far through a budget's money this month to warn, as a percentage, 1 to 100.
    /// </summary>
    public int BudgetWarningPercent { get; set; }

    /// <summary>
    /// How many days before a bill is due to remind them, 1 to <see cref="MostBillReminderDays"/>.
    /// </summary>
    public int BillReminderDays { get; set; }

    /// <summary>
    /// Tell them about money going out over this, null for never.
    /// </summary>
    public decimal? LargeTransactionOver { get; set; }

    /// <summary>
    /// The accounts they're notified of running low, with how low each can go. Those left out aren't watched.
    /// </summary>
    public List<LowBalanceThreshold> LowBalance { get; set; } = [];
}
