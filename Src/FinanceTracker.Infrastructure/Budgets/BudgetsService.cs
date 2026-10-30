using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
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
    /// Today where the server is.
    /// </summary>
    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private BudgetMonth CurrentMonth => BudgetMonth.Of(Today);

    public async Task<BudgetMonthOverview> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        BudgetMonth target = new(year, month);

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<DomainBudget> running = (await context.Budgets.OwnedBy(UserId).AsNoTracking()
                .ToListAsync(cancellationToken))
            .Where(x => x.FirstMonth <= target)
            .ToList();

        List<BankTransaction> transactions = await LoadTransactionsAsync(context, running, target, cancellationToken);
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;
        DateOnly today = Today;

        List<BudgetStatus> statuses = running
            .Select(budget => BudgetMapper.ToContract(budget,
                BudgetCalculator.ForMonth(budget, transactions, target, timeZone, today)!))
            .OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new BudgetMonthOverview
        {
            Month = target.FirstDay,
            IsCurrent = target == BudgetMonth.Of(today),
            Budgets = statuses,
            Unbudgeted = BudgetCalculator.Unbudgeted(running, transactions, target, timeZone)
        };
    }

    public async IAsyncEnumerable<Budget> GetBudgetsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Names are encrypted, so they're ordered once decrypted
        List<DomainBudget> budgets = await context.Budgets.OwnedBy(UserId).AsNoTracking()
            .ToListAsync(cancellationToken);
        BudgetMonth currentMonth = CurrentMonth;

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

        return BudgetMapper.ToContract(budget, CurrentMonth);
    }

    public async Task<Budget> AddBudgetAsync(SaveBudget budget, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        await EnsureTagsAreTheUsersAsync(context, budget.TagIds, cancellationToken);

        BudgetMonth currentMonth = CurrentMonth;

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
        BudgetMonth currentMonth = CurrentMonth;
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
    /// The user's transactions from the earliest running budget's first month to the end of <paramref name="target"/>,
    /// a day either side for time zones. Dates are encrypted, so this narrows by their day index.
    /// </summary>
    private async Task<List<BankTransaction>> LoadTransactionsAsync(FinanceTrackerContext context,
        List<DomainBudget> running, BudgetMonth target, CancellationToken cancellationToken)
    {
        BudgetMonth first = target;
        foreach (DomainBudget budget in running)
        {
            if (budget.FirstMonth < first)
            {
                first = budget.FirstMonth;
            }
        }

        DateTime from = first.FirstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);
        DateTime to = target.Next.FirstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);

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
