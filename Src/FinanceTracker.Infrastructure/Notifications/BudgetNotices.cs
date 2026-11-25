using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Common;
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
    private readonly IBudgetsService _budgetsService;
    private readonly INotifier _notifier;

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="budgetsService">Gives the budgets.</param>
    /// <param name="notifier">Raises notifications.</param>
    public BudgetNotices(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<BudgetNotices> logger,
        TimeProvider timeProvider, IBudgetsService budgetsService, INotifier notifier)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _budgetsService = budgetsService;
        _notifier = notifier;
    }

    /// <inheritdoc/>
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

        BudgetMonthOverview overview = await _budgetsService.GetMonthAsync(month.Year, month.Month, cancellationToken);
        foreach (BudgetStatus budget in overview.Budgets)
        {
            await NotifyForAsync(budget, month, warningPercent, cancellationToken);
        }
    }

    /// <summary>
    /// Says it's over when it is, otherwise that it's near once it reaches <paramref name="warningPercent"/> of what
    /// it has. A budget with nothing to spend is only ever over.
    /// </summary>
    /// <param name="budget">How the budget is doing.</param>
    /// <param name="month">The month.</param>
    /// <param name="warningPercent">How much of the budget is spent before warning, as a percentage.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task NotifyForAsync(BudgetStatus budget, BudgetMonth month, int warningPercent,
        CancellationToken cancellationToken)
    {
        string spent = $"{NoticeText.Money(budget.Spent)} of {NoticeText.Money(budget.Available)} spent this month.";
        string name = NoticeText.Named(budget.Name, budget.SharedBy);

        if (budget.Spent > budget.Available)
        {
            await _notifier.NotifyWithAmountsAsync(NotificationKind.BudgetOverLimit, Key("budget-over", budget, month),
                $"{name} is {NoticeText.Money(budget.Spent - budget.Available)} over", $"{name} is over budget",
                spent, BudgetsLink, cancellationToken);
            return;
        }

        if (budget.Available > 0 && budget.Spent >= budget.Available * warningPercent / 100)
        {
            await _notifier.NotifyWithAmountsAsync(NotificationKind.BudgetNearLimit, Key("budget-near", budget, month),
                $"{name} has {NoticeText.Money(budget.Remaining)} left", $"{name} is nearly used up", spent,
                BudgetsLink, cancellationToken);
        }
    }

    /// <summary>
    /// Keyed by the budget and the month, so each is said once a month.
    /// </summary>
    /// <param name="what">What the notification is about.</param>
    /// <param name="budget">The budget.</param>
    /// <param name="month">The month.</param>
    private static string Key(string what, BudgetStatus budget, BudgetMonth month) =>
        $"{what}:{budget.BudgetId:N}:{month.Year:D4}{month.Month:D2}";

    /// <summary>
    /// The month the user's current period is named after, from pay to pay or a calendar month.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<BudgetMonth> CurrentMonthAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        DateOnly today = _timeProvider.Today;
        PayCalendar calendar = await PayCalendars.ForUserAsync(context, UserId, today, cancellationToken);
        return BudgetCalculator.MonthOf(today, calendar);
    }

    /// <summary>
    /// How much of a budget is spent before warning, the default when the user has never changed it.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<int> WarningPercentAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        NotificationSettings? settings = await context.NotificationSettings.OwnedBy(UserId).AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return settings?.BudgetWarningPercent ?? NotificationSettings.DefaultBudgetWarningPercent;
    }
}
