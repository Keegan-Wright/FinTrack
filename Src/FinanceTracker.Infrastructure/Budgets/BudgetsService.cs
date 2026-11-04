using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.PayPeriods;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Budget = FinanceTracker.Contracts.Budgets.Budget;
using DomainBudget = FinanceTracker.Domain.Budgeting.Budget;

namespace FinanceTracker.Infrastructure.Budgets;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IBudgetsService>]
public class BudgetsService : ServiceBase<BudgetsService>, IBudgetsService
{
    private readonly TimeProvider _timeProvider;

    public BudgetsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<BudgetsService> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// The month today's period is named after, which can be the next calendar month once the pay is in, e.g.
    /// November from pay on 30 October.
    /// </summary>
    private async Task<BudgetMonth> CurrentMonthAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        DateOnly today = _timeProvider.Today;
        PayCalendar calendar = await PayCalendars.ForUserAsync(context, UserId, today, cancellationToken);
        return BudgetCalculator.MonthOf(today, calendar);
    }

    public async Task<BudgetMonthOverview> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        BudgetMonth target = new(year, month);

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<DomainBudget> running = (await context.Budgets.OwnedBy(UserId).AsNoTracking()
                .ToListAsync(cancellationToken))
            .Where(x => x.FirstMonth <= target)
            .ToList();

        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;
        DateOnly today = _timeProvider.Today;
        PayCalendar calendar = await PayCalendars.ForUserAsync(context, UserId, today, cancellationToken);
        List<BankTransaction> transactions =
            await LoadTransactionsAsync(context, running, target, calendar, cancellationToken);

        List<BudgetStatus> statuses = running
            .Select(budget => BudgetMapper.ToContract(budget,
                BudgetCalculator.ForMonth(budget, transactions, target, timeZone, today, calendar)!))
            .OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        PayPeriod period = calendar.Named(target.Year, target.Month);
        return new BudgetMonthOverview
        {
            Month = target.FirstDay,
            Start = period.Start,
            End = period.End,
            IsCurrent = target == BudgetCalculator.MonthOf(today, calendar),
            Budgets = statuses,
            Unbudgeted = BudgetCalculator.Unbudgeted(running, transactions, target, timeZone, calendar)
        };
    }

    public async IAsyncEnumerable<Budget> GetBudgetsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Names are encrypted, so they're ordered once decrypted
        List<DomainBudget> budgets = await context.Budgets.OwnedBy(UserId).AsNoTracking()
            .ToListAsync(cancellationToken);
        BudgetMonth currentMonth = await CurrentMonthAsync(context, cancellationToken);

        foreach (DomainBudget budget in budgets.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return BudgetMapper.ToContract(budget, currentMonth);
        }
    }

    public async Task<Budget> GetBudgetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBudget budget = await context.Budgets.OwnedBy(UserId).AsNoTracking()
            .SingleAsync(x => x.Id == id, cancellationToken);

        return BudgetMapper.ToContract(budget, await CurrentMonthAsync(context, cancellationToken));
    }

    public async Task<Budget> AddBudgetAsync(SaveBudget budget, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        await EnsureTagsAreTheUsersAsync(context, budget.TagIds, cancellationToken);

        BudgetMonth currentMonth = await CurrentMonthAsync(context, cancellationToken);

        DomainBudget newBudget = new(UserId, budget.Name, budget.TagIds, budget.MonthlyLimit, currentMonth,
            BudgetMapper.ToDomain(budget.RolloverMode));

        context.Budgets.Add(newBudget);
        await context.SaveChangesAsync(cancellationToken);

        return BudgetMapper.ToContract(newBudget, currentMonth);
    }

    public async Task<Budget> UpdateBudgetAsync(Guid id, SaveBudget budget, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBudget updated = await context.Budgets.OwnedBy(UserId).SingleAsync(x => x.Id == id, cancellationToken);
        await EnsureTagsAreTheUsersAsync(context, budget.TagIds, cancellationToken);

        // From this month on, the months before keep the limit they had
        BudgetMonth currentMonth = await CurrentMonthAsync(context, cancellationToken);
        updated.Rename(budget.Name);
        updated.ReplaceTags(budget.TagIds);
        updated.ChangeRolloverMode(BudgetMapper.ToDomain(budget.RolloverMode));
        updated.ChangeLimit(budget.MonthlyLimit, currentMonth);

        await context.SaveChangesAsync(cancellationToken);

        return BudgetMapper.ToContract(updated, currentMonth);
    }

    public async Task DeleteBudgetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBudget budget = await context.Budgets.OwnedBy(UserId).SingleAsync(x => x.Id == id, cancellationToken);
        context.Budgets.Remove(budget);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The user's transactions from the start of the earliest running budget's first period to the end of
    /// <paramref name="target"/>'s, a day either side for time zones. Dates are encrypted, so this narrows by their
    /// day index.
    /// </summary>
    private async Task<List<BankTransaction>> LoadTransactionsAsync(FinanceTrackerContext context,
        List<DomainBudget> running, BudgetMonth target, PayCalendar calendar, CancellationToken cancellationToken)
    {
        BudgetMonth first = target;
        foreach (DomainBudget budget in running)
        {
            if (budget.FirstMonth < first)
            {
                first = budget.FirstMonth;
            }
        }

        DateOnly start = calendar.Named(first.Year, first.Month).Start;
        DateOnly end = calendar.Named(target.Year, target.Month).End;
        DateTime from = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);
        DateTime to = end.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(2);

        return await context.BankTransactions.OwnedBy(UserId)
            .WhereEncryptedDayBetween(x => x.OccurredAt, from, to, context)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <exception cref="InvalidOperationException">A tag doesn't exist or isn't the user's.</exception>
    private async Task EnsureTagsAreTheUsersAsync(FinanceTrackerContext context, List<Guid> tagIds,
        CancellationToken cancellationToken)
    {
        List<Guid> distinct = tagIds.Distinct().ToList();

        int owned = await context.CustomClassifications.OwnedBy(UserId)
            .CountAsync(x => distinct.Contains(x.Id), cancellationToken);

        if (owned != distinct.Count)
        {
            throw new InvalidOperationException("A budget can only cover the user's own tags.");
        }
    }
}
