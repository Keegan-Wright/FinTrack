using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.PayPeriods;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Tells the current user when a budget's spending this month reaches the percentage they're warned at, and when it
/// goes over.
/// </summary>
/// <remarks>
/// Works from the Budgets page's own figures for the current month, so what's said matches what's shown. Keyed by
/// the budget and the month, so each is said once a month.
/// </remarks>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISyncNotices>]
public sealed class BudgetNotices : ServiceBase<BudgetNotices>, ISyncNotices
{
    /// <summary>
    /// Where budgets are dealt with.
    /// </summary>
    public const string BudgetsLink = "Budgets";

    private readonly TimeProvider _timeProvider;
    private readonly IBudgetsService _budgets;
    private readonly INotifier _notifier;

    public BudgetNotices(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<BudgetNotices> logger,
        TimeProvider timeProvider, IBudgetsService budgets, INotifier notifier)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _budgets = budgets;
        _notifier = notifier;
    }

    public async Task NotifyAsync(CancellationToken cancellationToken)
    {
        BudgetMonth month;
        int warningPercent;
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            month = await CurrentMonthAsync(context, cancellationToken);
            warningPercent = await WarningPercentAsync(context, cancellationToken);
        }

        BudgetMonthOverview overview = await _budgets.GetMonthAsync(month.Year, month.Month, cancellationToken);
        foreach (BudgetStatus budget in overview.Budgets)
        {
            await NotifyForAsync(budget, month, warningPercent, cancellationToken);
        }
    }

    /// <summary>
    /// Says it's over when it is, otherwise that it's near once it reaches <paramref name="warningPercent"/> of what
    /// it has. A budget with nothing to spend is only ever over.
    /// </summary>
    private async Task NotifyForAsync(BudgetStatus budget, BudgetMonth month, int warningPercent,
        CancellationToken cancellationToken)
    {
        string spent = $"{NoticeText.Money(budget.Spent)} of {NoticeText.Money(budget.Available)} spent this month.";
        string name = NoticeText.Named(budget.Name, budget.SharedBy);

        if (budget.Spent > budget.Available)
        {
            await _notifier.NotifyAsync(NotificationKind.BudgetOverLimit, Key("budget-over", budget, month),
                $"{name} is {NoticeText.Money(budget.Spent - budget.Available)} over", spent, BudgetsLink,
                cancellationToken);
            return;
        }

        if (budget.Available > 0 && budget.Spent >= budget.Available * warningPercent / 100)
        {
            await _notifier.NotifyAsync(NotificationKind.BudgetNearLimit, Key("budget-near", budget, month),
                $"{name} has {NoticeText.Money(budget.Remaining)} left", spent, BudgetsLink,
                cancellationToken);
        }
    }

    private static string Key(string what, BudgetStatus budget, BudgetMonth month) =>
        $"{what}:{budget.BudgetId:N}:{month.Year:D4}{month.Month:D2}";

    /// <summary>
    /// The month the user's current period is named after, from pay to pay or a calendar month.
    /// </summary>
    private async Task<BudgetMonth> CurrentMonthAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        DateOnly today = _timeProvider.Today;
        PayCalendar calendar = await PayCalendars.ForUserAsync(context, UserId, today, cancellationToken);
        return BudgetCalculator.MonthOf(today, calendar);
    }

    private async Task<int> WarningPercentAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        NotificationSettings? settings = await context.NotificationSettings.OwnedBy(UserId).AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return settings?.BudgetWarningPercent ?? NotificationSettings.DefaultBudgetWarningPercent;
    }
}
