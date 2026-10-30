using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainSavingsGoal = FinanceTracker.Domain.SavingsGoals.SavingsGoal;
using DomainSavingsGoalSource = FinanceTracker.Domain.SavingsGoals.SavingsGoalSource;
using SavingsGoal = FinanceTracker.Contracts.SavingsGoals.SavingsGoal;

namespace FinanceTracker.Infrastructure.SavingsGoals;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISavingsGoalsService>]
public class SavingsGoalsService : ServiceBase<SavingsGoalsService>, ISavingsGoalsService
{
    private readonly TimeProvider _timeProvider;

    public SavingsGoalsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<SavingsGoalsService> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    public async IAsyncEnumerable<SavingsGoal> GetSavingsGoalsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Names are encrypted, so they're ordered once decrypted
        List<DomainSavingsGoal> goals = await context.SavingsGoals.OwnedBy(UserId).AsNoTracking()
            .ToListAsync(cancellationToken);

        List<SavingsGoal> mapped = await ToContractsAsync(context, goals, cancellationToken);
        foreach (SavingsGoal goal in mapped.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return goal;
        }
    }

    public async Task<SavingsGoal> GetSavingsGoalAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DomainSavingsGoal goal = await context.SavingsGoals.OwnedBy(UserId).AsNoTracking()
            .SingleAsync(x => x.Id == id, cancellationToken);

        return (await ToContractsAsync(context, [goal], cancellationToken)).Single();
    }

    public async IAsyncEnumerable<SavingsGoalAccount> GetSavingsAccountsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<BankAccount> accounts = await context.BankAccounts.OwnedBy(UserId).AsNoTracking()
            .Where(static x => x.Kind != BankAccountKind.Card)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, Guid> backing = await context.SavingsGoals.OwnedBy(UserId)
            .Where(static x => x.AccountId != null)
            .ToDictionaryAsync(static x => x.AccountId!.Value, static x => x.Id, cancellationToken);

        foreach (BankAccount account in accounts.OrderBy(static x => x.DisplayName, StringComparer.CurrentCulture))
        {
            yield return new SavingsGoalAccount
            {
                AccountId = account.Id,
                Name = account.DisplayName,
                GoalId = backing.TryGetValue(account.Id, out Guid goalId) ? goalId : null
            };
        }
    }

    public async Task<SavingsGoal> AddSavingsGoalAsync(SaveSavingsGoal goal, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureSourceIsTheUsersAsync(context, null, goal, cancellationToken);

        DomainSavingsGoal added = new(UserId, goal.Name, goal.Saved, goal.MonthlyContribution, goal.Target,
            goal.TargetDate);
        Track(added, goal);

        context.SavingsGoals.Add(added);
        await context.SaveChangesAsync(cancellationToken);

        return (await ToContractsAsync(context, [added], cancellationToken)).Single();
    }

    public async Task<SavingsGoal> UpdateSavingsGoalAsync(Guid id, SaveSavingsGoal goal,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainSavingsGoal updated = await context.SavingsGoals.OwnedBy(UserId)
            .SingleAsync(x => x.Id == id, cancellationToken);
        await EnsureSourceIsTheUsersAsync(context, id, goal, cancellationToken);

        updated.Rename(goal.Name);
        updated.ChangeMonthlyContribution(goal.MonthlyContribution);
        updated.SetTarget(goal.Target, goal.TargetDate);
        Track(updated, goal);

        await context.SaveChangesAsync(cancellationToken);

        return (await ToContractsAsync(context, [updated], cancellationToken)).Single();
    }

    public async Task<bool> DeleteSavingsGoalAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DomainSavingsGoal? savingsGoal = await context.SavingsGoals.OwnedBy(UserId)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (savingsGoal == null)
        {
            return false;
        }

        context.SavingsGoals.Remove(savingsGoal);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void Track(DomainSavingsGoal target, SaveSavingsGoal goal)
    {
        switch (SavingsGoalMapper.ToDomain(goal.Source))
        {
            case DomainSavingsGoalSource.Account:
                target.TrackAccount(goal.AccountId!.Value);
                break;
            case DomainSavingsGoalSource.Tags:
                target.TrackTags(goal.TagIds, goal.Saved, goal.TrackedFrom!.Value);
                break;
            default:
                target.TrackManually(goal.Saved);
                break;
        }
    }

    /// <summary>
    /// Each goal with what it's saved: the backing account's balance, or the tagged transactions since it started
    /// counting.
    /// </summary>
    private async Task<List<SavingsGoal>> ToContractsAsync(FinanceTrackerContext context,
        List<DomainSavingsGoal> goals, CancellationToken cancellationToken)
    {
        Dictionary<Guid, BankAccount> accounts = await LoadAccountsAsync(context, goals, cancellationToken);
        List<BankTransaction> transactions = await LoadTaggedTransactionsAsync(context, goals, cancellationToken);
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        List<SavingsGoal> mapped = [];
        foreach (DomainSavingsGoal goal in goals)
        {
            BankAccount? account = goal.AccountId is { } accountId ? accounts.GetValueOrDefault(accountId) : null;
            SavingsGoalProgress progress = SavingsGoalCalculator.Progress(goal, account?.Balance?.Current,
                transactions, timeZone, today);
            mapped.Add(SavingsGoalMapper.ToContract(goal, progress, account?.DisplayName));
        }

        return mapped;
    }

    private async Task<Dictionary<Guid, BankAccount>> LoadAccountsAsync(FinanceTrackerContext context,
        List<DomainSavingsGoal> goals, CancellationToken cancellationToken)
    {
        List<Guid> accountIds = goals
            .Select(static x => x.AccountId)
            .OfType<Guid>()
            .ToList();

        if (accountIds.Count == 0)
        {
            return [];
        }

        return await context.BankAccounts.OwnedBy(UserId).AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, cancellationToken);
    }

    /// <summary>
    /// The user's transactions from the earliest day a tag goal counts from, a day early for time zones. Dates are
    /// encrypted, so this narrows by their day index.
    /// </summary>
    private async Task<List<BankTransaction>> LoadTaggedTransactionsAsync(FinanceTrackerContext context,
        List<DomainSavingsGoal> goals, CancellationToken cancellationToken)
    {
        List<DateOnly> starts = goals
            .Where(static x => x.Source == DomainSavingsGoalSource.Tags && !x.CoversNothing)
            .Select(static x => x.TrackedFrom)
            .OfType<DateOnly>()
            .ToList();

        if (starts.Count == 0)
        {
            return [];
        }

        DateTime from = starts.Min().ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);
        DateTime to = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1);

        return await context.BankTransactions.OwnedBy(UserId)
            .WhereEncryptedDayBetween(x => x.OccurredAt, from, to, context)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <exception cref="InvalidOperationException">The account isn't the user's, is a card or backs another goal, or a
    /// tag isn't the user's.</exception>
    private async Task EnsureSourceIsTheUsersAsync(FinanceTrackerContext context, Guid? goalId, SaveSavingsGoal goal,
        CancellationToken cancellationToken)
    {
        if (goal.Source == Contracts.SavingsGoals.SavingsGoalSource.Account)
        {
            await EnsureAccountIsFreeAsync(context, goalId, goal.AccountId!.Value, cancellationToken);
        }

        if (goal.Source == Contracts.SavingsGoals.SavingsGoalSource.Tags)
        {
            await EnsureTagsAreTheUsersAsync(context, goal.TagIds, cancellationToken);
        }
    }

    private async Task EnsureAccountIsFreeAsync(FinanceTrackerContext context, Guid? goalId, Guid accountId,
        CancellationToken cancellationToken)
    {
        bool usable = await context.BankAccounts.OwnedBy(UserId)
            .AnyAsync(x => x.Id == accountId && x.Kind != BankAccountKind.Card, cancellationToken);
        if (!usable)
        {
            throw new InvalidOperationException("A goal can only track one of the user's accounts that isn't a card.");
        }

        bool taken = await context.SavingsGoals.OwnedBy(UserId)
            .AnyAsync(x => x.AccountId == accountId && x.Id != goalId, cancellationToken);
        if (taken)
        {
            throw new InvalidOperationException("That account already backs another goal.");
        }
    }

    private async Task EnsureTagsAreTheUsersAsync(FinanceTrackerContext context, List<Guid> tagIds,
        CancellationToken cancellationToken)
    {
        List<Guid> distinct = tagIds.Distinct().ToList();

        int owned = await context.CustomClassifications.OwnedBy(UserId)
            .CountAsync(x => distinct.Contains(x.Id), cancellationToken);

        if (owned != distinct.Count)
        {
            throw new InvalidOperationException("A goal can only track the user's own tags.");
        }
    }
}
