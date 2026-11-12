using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Debt = FinanceTracker.Contracts.Debts.Debt;
using DebtForecast = FinanceTracker.Contracts.Debts.DebtForecast;
using DomainDebt = FinanceTracker.Domain.Debts.Debt;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Infrastructure.Debts;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IDebtsService>]
public class DebtsService : ServiceBase<DebtsService>, IDebtsService
{
    private readonly TimeProvider _timeProvider;
    private readonly IAccountScope _accountScope;

    public DebtsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<DebtsService> logger,
        TimeProvider timeProvider, IAccountScope accountScope) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _accountScope = accountScope;
    }

    public async IAsyncEnumerable<Debt> GetDebtsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own and those shared with them. Names are encrypted, so they're ordered once decrypted
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        List<VisibleDebt> debts = await SharedDebts.ForAsync(context, UserId, all, cancellationToken);
        Dictionary<Guid, DebtFigures> figures = await SharedDebts.FiguresAsync(context, all, debts, cancellationToken);
        Dictionary<Guid, string> accountNames = await AccountNamesAsync(context, all, cancellationToken);

        foreach (VisibleDebt debt in debts.OrderBy(static x => x.Debt.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return ToContract(debt, figures[debt.Debt.Id], accountNames);
        }
    }

    public async Task<DebtDetail> GetDebtAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        VisibleDebt visible = await VisibleAsync(context, all, id, mustChange: false, cancellationToken);
        DebtFigures figures = (await SharedDebts.FiguresAsync(context, all, [visible], cancellationToken))[id];
        Dictionary<Guid, string> accountNames = await AccountNamesAsync(context, all, cancellationToken);

        // Its figures come from all its payments, but only those on accounts the user sees are listed
        List<BankTransaction> seen = await context.BankTransactions.InView(all).AsNoTracking()
            .Where(x => x.DebtPayment != null && x.DebtPayment.DebtId == id)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, string> ruleNames = await LoadRuleNamesAsync(context, [visible.Debt.UserId, UserId], seen,
            cancellationToken);

        // Dates are encrypted, so they're ordered once decrypted
        List<DebtPaymentItem> items = seen
            .OrderByDescending(static x => x.OccurredAt)
            .Select(x => DebtMapper.ToItem(x, accountNames.GetValueOrDefault(x.AccountId) ?? string.Empty,
                RuleName(ruleNames, x.DebtPayment!.RuleId)))
            .ToList();

        IReadOnlyList<DebtBalance> history = await HistoryAsync(context, visible.Debt, figures.Payments,
            cancellationToken);

        return new DebtDetail
        {
            Debt = ToContract(visible, figures, accountNames),
            Payments = items,
            History = history.Select(DebtMapper.ToContract).ToList()
        };
    }

    public async Task<Debt> AddDebtAsync(SaveDebt debt, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        await EnsureNameIsFreeAsync(context, UserId, debt.Name, null, cancellationToken);

        DomainDebt added = new(UserId, debt.Name, debt.Lender, debt.StartingBalance, debt.StartedOn,
            debt.InterestRate, debt.MonthlyPayment);
        added.SetPayOffGoal(debt.PayOffGoal);
        await LinkAsync(context, added, debt.AccountId, cancellationToken);

        context.Debts.Add(added);
        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.Debt, debt.Name, cancellationToken);
        Logger.LogChanged(UserId, "created", "debt", added.Id);

        return await ToContractAsync(context, new VisibleDebt(added, null, true), cancellationToken);
    }

    public async Task<Debt> UpdateDebtAsync(Guid id, SaveDebt debt, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one shared with them with changes allowed, which keeps the account its owner chose
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        VisibleDebt visible = await VisibleAsync(context, all, id, mustChange: true, cancellationToken);
        DomainDebt updated = await context.Debts.SingleAsync(x => x.Id == id, cancellationToken);
        if (!visible.IsOwn && debt.AccountId != updated.AccountId)
        {
            throw new InvalidOperationException("Only its owner can change which account a shared debt follows.");
        }

        await EnsureNameIsFreeAsync(context, updated.UserId, debt.Name, id, cancellationToken);
        updated.Rename(debt.Name);
        updated.ChangeLender(debt.Lender);
        updated.ChangeStartingBalance(debt.StartingBalance, debt.StartedOn);
        updated.ChangeInterestRate(debt.InterestRate);
        updated.ChangeMonthlyPayment(debt.MonthlyPayment);
        updated.SetPayOffGoal(debt.PayOffGoal);

        if (visible.IsOwn)
        {
            await LinkAsync(context, updated, debt.AccountId, cancellationToken);
        }

        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.Debt, debt.Name, cancellationToken);
        Logger.LogChanged(UserId, "updated", "debt", id);

        return await ToContractAsync(context, visible with { Debt = updated }, cancellationToken);
    }

    /// <summary>
    /// Paid off debts count, as their payments still name them.
    /// </summary>
    /// <exception cref="NameTakenException">Another of <paramref name="ownerId"/>'s debts is called
    /// <paramref name="name"/>.</exception>
    private static async Task EnsureNameIsFreeAsync(FinanceTrackerContext context, Guid ownerId, string name,
        Guid? exceptId, CancellationToken cancellationToken)
    {
        List<DomainDebt> existing = await context.Debts.OwnedBy(ownerId).AsNoTracking().ToListAsync(cancellationToken);
        NameCheck.EnsureFree(existing.Select(static x => (x.Id, x.Name)), name, exceptId, NameTakenException.Debt);
    }

    public async Task DeleteDebtAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainDebt debt = await context.Debts.OwnedBy(UserId).SingleAsync(x => x.Id == id, cancellationToken);

        // Whoever's transaction pays it, they may have let the user say theirs does
        List<BankTransaction> payments = await context.BankTransactions
            .Where(x => x.DebtPayment != null && x.DebtPayment.DebtId == id)
            .ToListAsync(cancellationToken);
        foreach (BankTransaction payment in payments)
        {
            payment.RemoveDeletedDebt(id);
        }

        // Action ids are inside each rule's JSON, so every rule is checked
        List<Rule> rules = await context.Rules.OwnedBy(UserId).ToListAsync(cancellationToken);
        int rulesChanged = 0;
        foreach (Rule rule in rules)
        {
            if (rule.RemoveDebt(id))
            {
                rulesChanged++;
            }
        }

        context.Debts.Remove(debt);
        await context.SaveChangesAsync(cancellationToken);

        Logger.LogDeletedDebt(UserId, id, payments.Count, rulesChanged);
    }

    public async Task<DebtForecast> ForecastAsync(Guid id, decimal monthlyPayment,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(monthlyPayment);

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        VisibleDebt visible = await VisibleAsync(context, all, id, mustChange: false, cancellationToken);
        DebtFigures figures = (await SharedDebts.FiguresAsync(context, all, [visible], cancellationToken))[id];
        DebtProgress progress = Progress(visible.Debt, figures);

        return DebtMapper.ToContract(DebtCalculator.Forecast(progress.Owed, visible.Debt.InterestRate,
            monthlyPayment, _timeProvider.Today));
    }

    public async Task<Transaction> PayDebtAsync(Guid transactionId, PayDebt payment,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one on an account shared with them with changes allowed, paying one of their own debts or one
        // shared with them. Their own is shared with the transaction's owner, so they see what it's called
        AccountsInView changeable = await _accountScope.ResolveChangeableAsync(context, cancellationToken);
        BankTransaction transaction = await context.BankTransactions.ChangeableIn(changeable)
            .SingleAsync(x => x.Id == transactionId, cancellationToken);

        if (payment.DebtId is not { } debtId)
        {
            transaction.PayNoDebt(_timeProvider.UtcNow);
        }
        else
        {
            AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
            VisibleDebt debt = await VisibleAsync(context, all, debtId, mustChange: false, cancellationToken);
            transaction.PayDebt(debt.Debt, UserId, _timeProvider.UtcNow, sharedWithThem: !debt.IsOwn);
            if (debt.IsOwn)
            {
                await AnnotationShares.ShareWithOwnerAsync(context, UserId, transaction, ShareItemType.Debt, debtId,
                    _timeProvider.UtcNow, cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "set the debt paid by", "transaction", transactionId);

        AccountsInView view = await _accountScope.ResolveAllAsync(context, cancellationToken);
        TransactionMapper mapper = await TransactionMapper.LoadAsync(context, view, [transaction],
            _timeProvider.LocalTimeZone, cancellationToken);

        return mapper.ToContract(transaction);
    }

    /// <summary>
    /// Links <paramref name="debt"/> to <paramref name="accountId"/>, or works it out from its payments again when
    /// null.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account doesn't exist or isn't the user's.</exception>
    private async Task LinkAsync(FinanceTrackerContext context, DomainDebt debt, Guid? accountId,
        CancellationToken cancellationToken)
    {
        if (accountId is not { } id)
        {
            debt.UnlinkAccount();
            return;
        }

        BankAccount? account = await context.BankAccounts.OwnedBy(UserId).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("A debt can only follow the user's own accounts.");
        }

        debt.LinkAccount(account);
    }

    /// <summary>
    /// The linked account's daily balances, the last of each month and the latest, or the debt worked out from its
    /// payments when it isn't linked.
    /// </summary>
    private async Task<IReadOnlyList<DebtBalance>> HistoryAsync(FinanceTrackerContext context, DomainDebt debt,
        List<BankTransaction> payments, CancellationToken cancellationToken)
    {
        DateOnly today = _timeProvider.Today;
        if (debt.AccountId is not { } accountId)
        {
            return DebtCalculator.History(debt, payments, _timeProvider.LocalTimeZone, today);
        }

        // The account it follows is always its owner's
        List<BalanceSnapshot> snapshots = await context.BalanceSnapshots.OwnedBy(debt.UserId).AsNoTracking()
            .Where(x => x.AccountId == accountId && x.Date >= debt.StartedOn && x.Date <= today)
            .ToListAsync(cancellationToken);

        List<BalanceSnapshot> ordered = snapshots.OrderBy(static x => x.Date).ToList();
        List<DebtBalance> history = [];

        for (int i = 0; i < ordered.Count; i++)
        {
            BalanceSnapshot snapshot = ordered[i];
            bool isLast = i == ordered.Count - 1;
            bool endsItsMonth = isLast || ordered[i + 1].Date.Month != snapshot.Date.Month ||
                                ordered[i + 1].Date.Year != snapshot.Date.Year;
            if (endsItsMonth)
            {
                history.Add(new DebtBalance(snapshot.Date, Math.Max(0, -snapshot.Balance.Current)));
            }
        }

        return history;
    }

    private DebtProgress Progress(DomainDebt debt, DebtFigures figures) =>
        DebtCalculator.Progress(debt, figures.Account, figures.Payments, _timeProvider.LocalTimeZone,
            _timeProvider.Today);

    private async Task<Debt> ToContractAsync(FinanceTrackerContext context, VisibleDebt debt,
        CancellationToken cancellationToken)
    {
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        DebtFigures figures = (await SharedDebts.FiguresAsync(context, all, [debt], cancellationToken))[debt.Debt.Id];
        Dictionary<Guid, string> accountNames = await AccountNamesAsync(context, all, cancellationToken);
        return ToContract(debt, figures, accountNames);
    }

    private Debt ToContract(VisibleDebt debt, DebtFigures figures, Dictionary<Guid, string> accountNames) =>
        DebtMapper.ToContract(debt.Debt, AccountNameOf(debt.Debt, accountNames), Progress(debt.Debt, figures),
            debt.SharedBy, debt.CanChange);

    /// <summary>
    /// The name of the account <paramref name="debt"/> follows, private when the user can't see it, null when it
    /// follows none.
    /// </summary>
    private static string? AccountNameOf(DomainDebt debt, Dictionary<Guid, string> accountNames)
    {
        if (debt.AccountId is not { } accountId)
        {
            return null;
        }

        return accountNames.GetValueOrDefault(accountId, Rules.SharedRules.PrivateAccountName);
    }

    /// <summary>
    /// The names of the accounts the user sees.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> AccountNamesAsync(FinanceTrackerContext context,
        AccountsInView all, CancellationToken cancellationToken) =>
        await context.BankAccounts.InView(all).AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);

    /// <summary>
    /// Debt <paramref name="id"/>, the user's own or one shared with them.
    /// </summary>
    /// <param name="mustChange">Whether they must be allowed to change it.</param>
    /// <exception cref="InvalidOperationException">They can't see it, or can't change it.</exception>
    private async Task<VisibleDebt> VisibleAsync(FinanceTrackerContext context, AccountsInView all, Guid id,
        bool mustChange, CancellationToken cancellationToken)
    {
        VisibleDebt? debt = (await SharedDebts.ForAsync(context, UserId, all, cancellationToken))
            .SingleOrDefault(x => x.Debt.Id == id);
        if (debt is null || (mustChange && !debt.CanChange))
        {
            throw new InvalidOperationException($"No debt {id} the user can see or change was found.");
        }

        return debt;
    }

    /// <summary>
    /// The names of the rules that linked <paramref name="payments"/>, <paramref name="people"/>'s, as a debt's
    /// owner's rules and those of anyone they let change its accounts may have. Empty when none did.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> LoadRuleNamesAsync(FinanceTrackerContext context,
        IReadOnlyCollection<Guid> people, List<BankTransaction> payments, CancellationToken cancellationToken)
    {
        if (!payments.Exists(static x => x.DebtPayment?.RuleId is not null))
        {
            return [];
        }

        // Names are encrypted, so they're read whole and decrypted
        List<Rule> rules = await context.Rules.OwnedByAny(people).AsNoTracking().ToListAsync(cancellationToken);
        return rules.ToDictionary(static x => x.Id, static x => x.Name);
    }

    private static string? RuleName(Dictionary<Guid, string> ruleNames, Guid? ruleId)
    {
        if (ruleId is not { } id)
        {
            return null;
        }

        return ruleNames.GetValueOrDefault(id);
    }
}
