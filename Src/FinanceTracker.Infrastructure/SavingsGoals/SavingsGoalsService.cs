using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.NetWorth;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Sharing;
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

        // Their own and those shared with them. Names are encrypted, so they're ordered once decrypted
        List<VisibleItem<DomainSavingsGoal>> goals = await VisibleAsync(context, cancellationToken);

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
        VisibleItem<DomainSavingsGoal> goal = await VisibleAsync(context, id, mustChange: false, cancellationToken);
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

    public async Task<List<BalancePoint>> GetSavingsGoalHistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Its owner's, whoever's looking, so it's the same for both
        DomainSavingsGoal goal = (await VisibleAsync(context, id, mustChange: false, cancellationToken)).Item;

        if (goal is { Source: DomainSavingsGoalSource.Account, AccountId: { } accountId })
        {
            List<BalanceSnapshot> balances = await context.BalanceSnapshots.OwnedBy(goal.UserId).AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .OrderBy(static x => x.Date)
                .ToListAsync(cancellationToken);

            return balances.Select(NetWorthService.ToPoint).ToList();
        }

        if (goal.Source != DomainSavingsGoalSource.Tags)
        {
            return [];
        }

        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(goal.UserId).AsNoTracking()
            .ToListAsync(cancellationToken);

        return SavingsGoalCalculator.History(goal, transactions, _timeProvider.LocalTimeZone, _timeProvider.Today)
            .Select(static x => new BalancePoint { Date = x.Date, Amount = x.Saved })
            .ToList();
    }

    public async Task<SavingsGoal> AddSavingsGoalAsync(SaveSavingsGoal goal, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureSourceIsTheUsersAsync(context, null, goal, cancellationToken);
        await SavingsGoalNames.EnsureFreeAsync(context, UserId, goal.Name, null, cancellationToken);

        DomainSavingsGoal added = new(UserId, goal.Name, goal.Saved, goal.MonthlyContribution, goal.Target,
            goal.TargetDate);
        Track(added, goal);

        context.SavingsGoals.Add(added);
        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.SavingsGoal, goal.Name,
            cancellationToken);
        Logger.LogChanged(UserId, "created", "savings goal", added.Id);

        return (await ToContractsAsync(context, [new VisibleItem<DomainSavingsGoal>(added, null, true)],
            cancellationToken)).Single();
    }

    public async Task<SavingsGoal> UpdateSavingsGoalAsync(Guid id, SaveSavingsGoal goal,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one shared with them with changes allowed, which keeps tracking what its owner chose
        VisibleItem<DomainSavingsGoal> visible = await VisibleAsync(context, id, mustChange: true, cancellationToken);
        DomainSavingsGoal updated = await context.SavingsGoals.SingleAsync(x => x.Id == id, cancellationToken);
        if (visible.IsOwn)
        {
            await EnsureSourceIsTheUsersAsync(context, id, goal, cancellationToken);
        }
        else
        {
            EnsureTrackingIsUnchanged(updated, goal);
        }

        await SavingsGoalNames.EnsureFreeAsync(context, updated.UserId, goal.Name, id, cancellationToken);

        updated.Rename(goal.Name);
        updated.ChangeMonthlyContribution(goal.MonthlyContribution);
        updated.SetTarget(goal.Target, goal.TargetDate);
        if (visible.IsOwn)
        {
            Track(updated, goal);
        }
        else if (updated.Source == DomainSavingsGoalSource.Manual)
        {
            updated.TrackManually(goal.Saved);
        }

        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.SavingsGoal, goal.Name,
            cancellationToken);
        Logger.LogChanged(UserId, "updated", "savings goal", id);

        return (await ToContractsAsync(context, [visible with { Item = updated }], cancellationToken)).Single();
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
        Logger.LogChanged(UserId, "deleted", "savings goal", id);
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
                target.TrackTags(goal.TagIds, goal.Saved, goal.TrackedFrom!.Value,
                    SavingsGoalMapper.ToDomain(goal.Direction));
                break;
            default:
                target.TrackManually(goal.Saved);
                break;
        }
    }

    /// <summary>
    /// Each goal with what it's saved: the backing account's balance, or its owner's tagged transactions since it
    /// started counting. The same for whoever's looking, though an account they can't see isn't named.
    /// </summary>
    private async Task<List<SavingsGoal>> ToContractsAsync(FinanceTrackerContext context,
        List<VisibleItem<DomainSavingsGoal>> goals, CancellationToken cancellationToken)
    {
        Dictionary<Guid, BankAccount> accounts = await LoadAccountsAsync(context, goals, cancellationToken);
        Dictionary<Guid, string> seenNames = await SeenAccountNamesAsync(context, cancellationToken);

        // The tags of someone else's, which are theirs
        List<Guid> othersTagIds = goals.Where(static x => !x.IsOwn).SelectMany(static x => x.Item.TagIds).ToList();
        Dictionary<Guid, string> othersTags = await context.CustomClassifications.AsNoTracking()
            .Where(x => othersTagIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;
        DateOnly today = _timeProvider.Today;

        List<SavingsGoal> mapped = [];
        foreach (IGrouping<Guid, VisibleItem<DomainSavingsGoal>> owner in goals.GroupBy(static x => x.Item.UserId))
        {
            List<BankTransaction> transactions = await LoadTaggedTransactionsAsync(context, owner.Key,
                owner.Select(static x => x.Item).ToList(), cancellationToken);
            foreach ((DomainSavingsGoal goal, string? sharedBy, bool canChange) in owner)
            {
                BankAccount? account = goal.AccountId is { } accountId ? accounts.GetValueOrDefault(accountId) : null;
                SavingsGoalProgress progress = SavingsGoalCalculator.Progress(goal, account?.Balance?.Current,
                    transactions, timeZone, today);
                Dictionary<Guid, string> tagNames = sharedBy is null
                    ? []
                    : goal.TagIds.Where(othersTags.ContainsKey).ToDictionary(static x => x, x => othersTags[x]);
                mapped.Add(SavingsGoalMapper.ToContract(goal, progress, AccountNameOf(goal, seenNames), sharedBy,
                    canChange, tagNames));
            }
        }

        return mapped;
    }

    /// <summary>
    /// The name of the account <paramref name="goal"/> tracks, private when the user can't see it, null when it tracks
    /// none.
    /// </summary>
    private static string? AccountNameOf(DomainSavingsGoal goal, Dictionary<Guid, string> seenNames)
    {
        if (goal.AccountId is not { } accountId)
        {
            return null;
        }

        return seenNames.GetValueOrDefault(accountId, Rules.SharedRules.PrivateAccountName);
    }

    private async Task<Dictionary<Guid, string>> SeenAccountNamesAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        AccountsInView all = await AccountScope.ResolveAllForAsync(context, UserId, cancellationToken);
        return await context.BankAccounts.InView(all).AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);
    }

    /// <summary>
    /// The accounts the goals track, whoever's they are, for their balances.
    /// </summary>
    private static async Task<Dictionary<Guid, BankAccount>> LoadAccountsAsync(FinanceTrackerContext context,
        List<VisibleItem<DomainSavingsGoal>> goals, CancellationToken cancellationToken)
    {
        List<Guid> accountIds = goals
            .Select(static x => x.Item.AccountId)
            .OfType<Guid>()
            .ToList();

        if (accountIds.Count == 0)
        {
            return [];
        }

        return await context.BankAccounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, cancellationToken);
    }

    /// <summary>
    /// <paramref name="ownerId"/>'s transactions from the earliest day one of their tag goals counts from, a day early
    /// for time zones. Dates are encrypted, so this narrows by their day index.
    /// </summary>
    private async Task<List<BankTransaction>> LoadTaggedTransactionsAsync(FinanceTrackerContext context,
        Guid ownerId, List<DomainSavingsGoal> goals, CancellationToken cancellationToken)
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
        DateTime to = _timeProvider.UtcNow.AddDays(1);

        return await context.BankTransactions.OwnedBy(ownerId)
            .WhereEncryptedDayBetween(x => x.OccurredAt, from, to, context)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Every goal the user sees, their own and those shared with them.
    /// </summary>
    private async Task<List<VisibleItem<DomainSavingsGoal>>> VisibleAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        AccountsInView all = await AccountScope.ResolveAllForAsync(context, UserId, cancellationToken);
        return await SharedGoals.ForAsync(context, UserId, all, cancellationToken);
    }

    /// <summary>
    /// Goal <paramref name="id"/>, the user's own or one shared with them.
    /// </summary>
    /// <param name="mustChange">Whether they must be allowed to change it.</param>
    /// <exception cref="InvalidOperationException">They can't see it, or can't change it.</exception>
    private async Task<VisibleItem<DomainSavingsGoal>> VisibleAsync(FinanceTrackerContext context, Guid id,
        bool mustChange, CancellationToken cancellationToken)
    {
        VisibleItem<DomainSavingsGoal>? goal = (await VisibleAsync(context, cancellationToken))
            .SingleOrDefault(x => x.Item.Id == id);
        if (goal is null || (mustChange && !goal.CanChange))
        {
            throw new InvalidOperationException($"No savings goal {id} the user can see or change was found.");
        }

        return goal;
    }

    /// <summary>
    /// Someone else's goal keeps tracking what its owner chose.
    /// </summary>
    /// <exception cref="InvalidOperationException">The edit tracks something else.</exception>
    private static void EnsureTrackingIsUnchanged(DomainSavingsGoal existing, SaveSavingsGoal goal)
    {
        bool sameSource = SavingsGoalMapper.ToDomain(goal.Source) == existing.Source;
        bool sameAccount = existing.Source != DomainSavingsGoalSource.Account || goal.AccountId == existing.AccountId;
        bool sameTags = existing.Source != DomainSavingsGoalSource.Tags ||
                        (goal.TagIds.ToHashSet().SetEquals(existing.TagIds) &&
                         goal.TrackedFrom == existing.TrackedFrom &&
                         SavingsGoalMapper.ToDomain(goal.Direction) == existing.Direction);
        if (!sameSource || !sameAccount || !sameTags)
        {
            throw new InvalidOperationException("Only its owner can change what a shared goal tracks.");
        }
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
