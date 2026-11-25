using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Common.Persistence;
using FinanceTracker.Infrastructure.PayPeriods;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Budget = FinanceTracker.Contracts.Budgets.Budget;
using DomainBudget = FinanceTracker.Domain.Budgeting.Budget;

namespace FinanceTracker.Infrastructure.Budgets;

/// <inheritdoc cref="IBudgetsService"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IBudgetsService>]
public class BudgetsService : ServiceBase<BudgetsService>, IBudgetsService
{
    private readonly TimeProvider _timeProvider;

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="timeProvider">The clock.</param>
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
    /// <param name="context">The context to work through.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<BudgetMonth> CurrentMonthAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        DateOnly today = _timeProvider.Today;
        PayCalendar calendar = await PayCalendars.ForUserAsync(context, UserId, today, cancellationToken);
        return BudgetCalculator.MonthOf(today, calendar);
    }

    /// <inheritdoc/>
    public async Task<BudgetMonthOverview> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        BudgetMonth target = new(year, month);

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own and those shared with them, in their own pay periods
        List<VisibleItem<DomainBudget>> running = (await VisibleAsync(context, cancellationToken))
            .Where(x => x.Item.FirstMonth <= target)
            .ToList();
        Dictionary<Guid, BudgetSharing> sharing = await SharingAsync(context, running, cancellationToken);

        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;
        DateOnly today = _timeProvider.Today;
        PayCalendar calendar = await PayCalendars.ForUserAsync(context, UserId, today, cancellationToken);
        List<DomainBudget> budgets = running.Select(static x => x.Item).ToList();
        Dictionary<Guid, BudgetSpending> spending =
            await SpendingAsync(context, running, sharing, target, calendar, cancellationToken);

        List<BudgetStatus> statuses = running
            .Select(x => BudgetMapper.ToContract(x.Item,
                BudgetCalculator.ForMonth(x.Item, spending[x.Item.Id].Transactions, target, timeZone, today,
                    calendar, spending[x.Item.Id].ShownCopies)!,
                sharing[x.Item.Id]))
            .OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // What's left unbudgeted is theirs, from their own spending and budgets
        List<DomainBudget> own = running.Where(static x => x.IsOwn).Select(static x => x.Item).ToList();
        List<BankTransaction> ownSpending =
            await LoadTransactionsAsync(context, OwnTransactions(context), budgets, target, calendar,
                cancellationToken);

        PayPeriod period = calendar.Named(target.Year, target.Month);
        return new BudgetMonthOverview
        {
            Month = target.FirstDay,
            Start = period.Start,
            End = period.End,
            IsCurrent = target == BudgetCalculator.MonthOf(today, calendar),
            Budgets = statuses,
            Unbudgeted = BudgetCalculator.Unbudgeted(own, ownSpending, target, timeZone, calendar)
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Budget> GetBudgetsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own and those shared with them. Names are encrypted, so they're ordered once decrypted
        List<VisibleItem<DomainBudget>> budgets = await VisibleAsync(context, cancellationToken);
        Dictionary<Guid, BudgetSharing> sharing = await SharingAsync(context, budgets, cancellationToken);
        BudgetMonth currentMonth = await CurrentMonthAsync(context, cancellationToken);

        foreach (VisibleItem<DomainBudget> budget in budgets
                     .OrderBy(static x => x.Item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return BudgetMapper.ToContract(budget.Item, currentMonth, sharing[budget.Item.Id]);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BudgetAccount> GetAccountsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        Dictionary<Guid, string> labels = await AccountLabelsAsync(context, UserId, cancellationToken);

        foreach ((Guid accountId, string name) in labels.OrderBy(static x => x.Value, StringComparer.CurrentCulture))
        {
            yield return new BudgetAccount { AccountId = accountId, Name = name };
        }
    }

    /// <inheritdoc/>
    public async Task<Budget> GetBudgetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        VisibleItem<DomainBudget> budget = await VisibleAsync(context, id, mustChange: false, cancellationToken);
        Dictionary<Guid, BudgetSharing> sharing = await SharingAsync(context, [budget], cancellationToken);

        return BudgetMapper.ToContract(budget.Item, await CurrentMonthAsync(context, cancellationToken),
            sharing[id]);
    }

    /// <inheritdoc/>
    public async Task<Budget> AddBudgetAsync(SaveBudget budget, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        await EnsureTagsAreTheUsersAsync(context, budget.TagIds, cancellationToken);


        Dictionary<Guid, string> accounts = await AccountLabelsAsync(context, UserId, cancellationToken);
        if (budget.AccountIds.Exists(x => !accounts.ContainsKey(x)))
        {
            throw new DomainRuleException("A budget can only cover accounts the user sees.");
        }

        BudgetMonth currentMonth = await CurrentMonthAsync(context, cancellationToken);

        DomainBudget newBudget = new(UserId, budget.Name, budget.TagIds, budget.AccountIds, budget.MonthlyLimit,
            currentMonth, BudgetMapper.ToDomain(budget.RolloverMode));
        newBudget.CountTransfersOut(budget.CountsTransfersOut && budget.AccountIds.Count > 0);

        context.Budgets.Add(newBudget);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "created", "budget", newBudget.Id);

        BudgetSharing sharing = new(null, true, false, [], [], NamesOf(newBudget.AccountIds, accounts), []);
        return BudgetMapper.ToContract(newBudget, currentMonth, sharing);
    }

    /// <inheritdoc/>
    public async Task<Budget> UpdateBudgetAsync(Guid id, SaveBudget budget, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one shared with them with changes allowed, covering only tags and accounts they may
        VisibleItem<DomainBudget> visible = await VisibleAsync(context, id, mustChange: true, cancellationToken);
        DomainBudget updated = await context.Budgets.SingleAsync(x => x.Id == id, cancellationToken);
        IReadOnlySet<Guid> choices = await TagChoicesForAsync(context, visible, cancellationToken);
        IReadOnlySet<Guid> accountChoices = await AccountChoicesForAsync(context, visible, cancellationToken);

        // From this month on, the months before keep the limit they had
        BudgetMonth currentMonth = await CurrentMonthAsync(context, cancellationToken);
        updated.Rename(budget.Name);
        updated.ReplaceCoverage(budget.TagIds, choices, budget.AccountIds, accountChoices);
        updated.CountTransfersOut(budget.CountsTransfersOut && budget.AccountIds.Count > 0);
        updated.ChangeRolloverMode(BudgetMapper.ToDomain(budget.RolloverMode));
        updated.ChangeLimit(budget.MonthlyLimit, currentMonth);

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "updated", "budget", id);

        Dictionary<Guid, BudgetSharing> sharing =
            await SharingAsync(context, [visible with { Item = updated }], cancellationToken);
        return BudgetMapper.ToContract(updated, currentMonth, sharing[id]);
    }

    /// <inheritdoc/>
    public async Task DeleteBudgetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBudget budget = await context.Budgets.OwnedBy(UserId)
            .SingleOrNotFoundAsync(x => x.Id == id, "budget", cancellationToken);
        context.Budgets.Remove(budget);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "deleted", "budget", id);
    }

    /// <summary>
    /// <paramref name="source"/>'s transactions from the start of the earliest running budget's first period to the
    /// end of <paramref name="target"/>'s, a day either side for time zones. Dates are encrypted, so this narrows by
    /// their day index.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="source">Whose transactions, the budget's owner.</param>
    /// <param name="running">The budgets running.</param>
    /// <param name="target">The month to work out.</param>
    /// <param name="calendar">The user's periods.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private static async Task<List<BankTransaction>> LoadTransactionsAsync(FinanceTrackerContext context,
        IQueryable<BankTransaction> source, List<DomainBudget> running, BudgetMonth target, PayCalendar calendar,
        CancellationToken cancellationToken)
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

        return await source
            .WhereEncryptedDayBetween(x => x.OccurredAt, from, to, context)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The user's own transactions.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    private IQueryable<BankTransaction> OwnTransactions(FinanceTrackerContext context) =>
        context.BankTransactions.OwnedBy(UserId);

    /// <summary>
    /// The transactions each of <paramref name="running"/> counts, by budget. The user's own for their own tag
    /// budget. A household budget's, or one covering accounts, from every account its owner sees, the same for whoever
    /// looks, so a joint account shared with them counts.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="running">The budgets running.</param>
    /// <param name="sharing">How each budget is shared.</param>
    /// <param name="target">The month to work out.</param>
    /// <param name="calendar">The user's periods.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<Dictionary<Guid, BudgetSpending>> SpendingAsync(FinanceTrackerContext context,
        List<VisibleItem<DomainBudget>> running, Dictionary<Guid, BudgetSharing> sharing, BudgetMonth target,
        PayCalendar calendar, CancellationToken cancellationToken)
    {
        Dictionary<Guid, BudgetSpending> spending = [];

        List<DomainBudget> ownOnly = running
            .Where(x => CountsOwnSpendingOnly(x.Item, sharing[x.Item.Id]))
            .Select(static x => x.Item)
            .ToList();
        if (ownOnly.Count > 0)
        {
            List<BankTransaction> own = await LoadTransactionsAsync(context, OwnTransactions(context), ownOnly,
                target, calendar, cancellationToken);
            foreach (DomainBudget budget in ownOnly)
            {
                spending[budget.Id] = new BudgetSpending(own, new Dictionary<Guid, Guid>());
            }
        }

        foreach (IGrouping<Guid, DomainBudget> owner in running
                     .Where(x => !CountsOwnSpendingOnly(x.Item, sharing[x.Item.Id]))
                     .Select(static x => x.Item)
                     .GroupBy(static x => x.UserId))
        {
            AccountsInView ownersView = await AccountScope.ResolveAllForAsync(context, owner.Key, cancellationToken);
            List<DomainBudget> budgets = owner.ToList();
            List<BankTransaction> inView = await LoadTransactionsAsync(context,
                context.BankTransactions.InView(ownersView), budgets, target, calendar, cancellationToken);
            foreach (DomainBudget budget in budgets)
            {
                spending[budget.Id] = new BudgetSpending(inView, ownersView.FillingAccounts);
            }
        }

        return spending;
    }

    /// <summary>
    /// Whether <paramref name="budget"/> counts only its owner's own transactions, their own tag budget, unshared.
    /// </summary>
    /// <param name="budget">The budget.</param>
    /// <param name="sharing">How it's shared.</param>
    private static bool CountsOwnSpendingOnly(DomainBudget budget, BudgetSharing sharing) =>
        !sharing.IsHousehold && budget.AccountIds.Count == 0;

    /// <summary>
    /// Every budget the user sees, their own and those shared with them.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private Task<List<VisibleItem<DomainBudget>>> VisibleAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken) =>
        SharedChosenItems.ForAsync(context, context.Budgets, UserId, ShareItemType.Budget, justMine: false,
            cancellationToken);

    /// <summary>
    /// Budget <paramref name="id"/>, the user's own or one shared with them.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="id">Its id.</param>
    /// <param name="mustChange">Whether they must be allowed to change it.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <exception cref="InvalidOperationException">They can't see it, or can't change it.</exception>
    private async Task<VisibleItem<DomainBudget>> VisibleAsync(FinanceTrackerContext context, Guid id,
        bool mustChange, CancellationToken cancellationToken)
    {
        VisibleItem<DomainBudget>? budget = (await VisibleAsync(context, cancellationToken))
            .SingleOrDefault(x => x.Item.Id == id);
        if (budget is null || (mustChange && !budget.CanChange))
        {
            throw new NotFoundException($"No budget {id} the user can see or change was found.");
        }

        return budget;
    }

    /// <summary>
    /// How each of <paramref name="budgets"/> is shared, by budget, with the names of its accounts. Their own is a
    /// household budget once they share it, someone else's always is.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="budgets">The budgets.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<Dictionary<Guid, BudgetSharing>> SharingAsync(FinanceTrackerContext context,
        IReadOnlyCollection<VisibleItem<DomainBudget>> budgets, CancellationToken cancellationToken)
    {
        Dictionary<Guid, Dictionary<Guid, string>> labelsByOwner = [];
        Dictionary<Guid, BudgetSharing> sharing = [];
        foreach (VisibleItem<DomainBudget> budget in budgets)
        {
            Dictionary<Guid, string> accountNames =
                await AccountNamesAsync(context, budget.Item, labelsByOwner, cancellationToken);

            if (budget.IsOwn)
            {
                bool shared = await SharedChosenItems.IsSharedAsync(context, UserId, ShareItemType.Budget,
                    budget.Item.Id, cancellationToken);
                sharing[budget.Item.Id] = new BudgetSharing(null, true, shared, [], [], accountNames, []);
                continue;
            }

            List<Guid> tagIds = budget.Item.TagIds.ToList();
            Dictionary<Guid, string> tagNames = await context.CustomClassifications.AsNoTracking()
                .Where(x => tagIds.Contains(x.Id))
                .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);
            Dictionary<Guid, string> choices = [];
            Dictionary<Guid, string> accountChoices = [];
            if (budget.CanChange)
            {
                choices = await TagChoicesAsync(context, budget.Item, cancellationToken);
                accountChoices = await AccountChoicesAsync(context, budget.Item, labelsByOwner, cancellationToken);
            }

            sharing[budget.Item.Id] = new BudgetSharing(budget.SharedBy, budget.CanChange, true, tagNames, choices,
                accountNames, accountChoices);
        }

        return sharing;
    }

    /// <summary>
    /// The names of <paramref name="budget"/>'s accounts its owner still sees, by id, as the user knows them where
    /// they see them too, otherwise as its owner does. One that's been removed, or its owner no longer sees, has none.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="budget">The budget.</param>
    /// <param name="labelsByOwner">Each person's account labels looked up so far, by person.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<Dictionary<Guid, string>> AccountNamesAsync(FinanceTrackerContext context,
        DomainBudget budget, Dictionary<Guid, Dictionary<Guid, string>> labelsByOwner,
        CancellationToken cancellationToken)
    {
        if (budget.AccountIds.Count == 0)
        {
            return [];
        }

        Dictionary<Guid, string> owners =
            await LabelsOfAsync(context, budget.UserId, labelsByOwner, cancellationToken);
        Dictionary<Guid, string> users = await LabelsOfAsync(context, UserId, labelsByOwner, cancellationToken);

        Dictionary<Guid, string> names = [];
        foreach (Guid accountId in budget.AccountIds.Where(owners.ContainsKey))
        {
            names[accountId] = users.GetValueOrDefault(accountId) ?? owners[accountId];
        }

        return names;
    }

    /// <summary>
    /// The accounts the user can have <paramref name="budget"/>, someone else's, cover, by id, those both they and its
    /// owner see, and those it already covers that its owner still sees.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="budget">The budget.</param>
    /// <param name="labelsByOwner">Each person's account labels looked up so far, by person.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<Dictionary<Guid, string>> AccountChoicesAsync(FinanceTrackerContext context,
        DomainBudget budget, Dictionary<Guid, Dictionary<Guid, string>> labelsByOwner,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, string> users = await LabelsOfAsync(context, UserId, labelsByOwner, cancellationToken);
        Dictionary<Guid, string> owners =
            await LabelsOfAsync(context, budget.UserId, labelsByOwner, cancellationToken);

        Dictionary<Guid, string> choices = users
            .Where(x => owners.ContainsKey(x.Key))
            .ToDictionary(static x => x.Key, static x => x.Value);

        foreach (Guid accountId in budget.AccountIds.Where(owners.ContainsKey))
        {
            choices.TryAdd(accountId, owners[accountId]);
        }

        return choices;
    }

    /// <summary>
    /// The accounts the user may give <paramref name="visible"/>, see <see cref="DomainBudget.ReplaceCoverage"/>. Any
    /// they see for their own budget, or those they and its owner both see for someone else's. Those it already
    /// covers stay allowed, so a removed account doesn't stop it being saved.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="visible">The budget, as the user sees it.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<IReadOnlySet<Guid>> AccountChoicesForAsync(FinanceTrackerContext context,
        VisibleItem<DomainBudget> visible, CancellationToken cancellationToken)
    {
        HashSet<Guid> choices = [.. visible.Item.AccountIds];
        if (visible.IsOwn)
        {
            choices.UnionWith((await AccountLabelsAsync(context, UserId, cancellationToken)).Keys);
            return choices;
        }

        choices.UnionWith((await AccountChoicesAsync(context, visible.Item, [], cancellationToken)).Keys);
        return choices;
    }

    /// <summary>
    /// <paramref name="userId"/>'s account labels, looked up once per person.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="userId">Whose accounts.</param>
    /// <param name="labelsByOwner">Each person's account labels looked up so far, by person.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private static async Task<Dictionary<Guid, string>> LabelsOfAsync(FinanceTrackerContext context, Guid userId,
        Dictionary<Guid, Dictionary<Guid, string>> labelsByOwner, CancellationToken cancellationToken)
    {
        if (!labelsByOwner.TryGetValue(userId, out Dictionary<Guid, string>? labels))
        {
            labels = await AccountLabelsAsync(context, userId, cancellationToken);
            labelsByOwner[userId] = labels;
        }

        return labels;
    }

    /// <summary>
    /// Every account <paramref name="userId"/> sees, their own and those shared with them, by what they call it, e.g.
    /// <c>Joint · Jane Doe's</c> for someone else's.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="userId">Whose accounts.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private static async Task<Dictionary<Guid, string>> AccountLabelsAsync(FinanceTrackerContext context,
        Guid userId, CancellationToken cancellationToken)
    {
        AccountsInView view = await AccountScope.ResolveAllForAsync(context, userId, cancellationToken);
        return await view.LabelsAsync(context, cancellationToken);
    }

    /// <summary>
    /// The names of <paramref name="accountIds"/> among <paramref name="labels"/>, by id.
    /// </summary>
    /// <param name="accountIds">The accounts.</param>
    /// <param name="labels">What each account the user sees is called, by id.</param>
    private static Dictionary<Guid, string> NamesOf(IEnumerable<Guid> accountIds, Dictionary<Guid, string> labels) =>
        accountIds.Where(labels.ContainsKey).ToDictionary(static x => x, x => labels[x]);

    /// <summary>
    /// The tags the user can have <paramref name="budget"/>, someone else's, cover, its owner's tags shared with them,
    /// and those it already covers.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="budget">The budget.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<Dictionary<Guid, string>> TagChoicesAsync(FinanceTrackerContext context, DomainBudget budget,
        CancellationToken cancellationToken)
    {
        Guid ownerId = budget.UserId;
        List<Guid> onIt = budget.TagIds.ToList();
        IQueryable<Guid> shared = context.ChosenSharesWith(UserId, ShareItemType.Classification)
            .Select(static x => x.ItemId);

        return await context.CustomClassifications.AsNoTracking()
            .Where(x => x.UserId == ownerId && (onIt.Contains(x.Id) || shared.Contains(x.Id)))
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);
    }

    /// <summary>
    /// The tags the user may give <paramref name="visible"/>. Their own for their own budget, or its owner's tags
    /// shared with them for someone else's, see <see cref="DomainBudget.ReplaceCoverage"/>.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="visible">The budget, as the user sees it.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<IReadOnlySet<Guid>> TagChoicesForAsync(FinanceTrackerContext context,
        VisibleItem<DomainBudget> visible, CancellationToken cancellationToken)
    {
        if (visible.IsOwn)
        {
            List<Guid> own = await context.CustomClassifications.OwnedBy(UserId).Select(static x => x.Id)
                .ToListAsync(cancellationToken);
            return own.ToHashSet();
        }

        Dictionary<Guid, string> shared = await TagChoicesAsync(context, visible.Item, cancellationToken);
        return shared.Keys.ToHashSet();
    }

    /// <param name="context">The context to work through.</param>
    /// <param name="tagIds">The tags.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <exception cref="InvalidOperationException">A tag doesn't exist or isn't the user's.</exception>
    private async Task EnsureTagsAreTheUsersAsync(FinanceTrackerContext context, List<Guid> tagIds,
        CancellationToken cancellationToken)
    {
        List<Guid> distinct = tagIds.Distinct().ToList();

        int owned = await context.CustomClassifications.OwnedBy(UserId)
            .CountAsync(x => distinct.Contains(x.Id), cancellationToken);

        if (owned != distinct.Count)
        {
            throw new DomainRuleException("A budget can only cover the user's own tags.");
        }
    }
}
