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

        // Names are encrypted, so they're ordered once decrypted
        List<DomainDebt> debts = await context.Debts.OwnedBy(UserId).AsNoTracking().ToListAsync(cancellationToken);
        List<BankTransaction> payments = await LoadPaymentsAsync(context, cancellationToken);
        Dictionary<Guid, BankAccount> accounts = await LoadAccountsAsync(context, cancellationToken);

        foreach (DomainDebt debt in debts.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return ToContract(debt, accounts, payments);
        }
    }

    public async Task<DebtDetail> GetDebtAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainDebt debt = await context.Debts.OwnedBy(UserId).AsNoTracking()
            .SingleAsync(x => x.Id == id, cancellationToken);
        List<BankTransaction> payments = await LoadPaymentsAsync(context, id, cancellationToken);
        Dictionary<Guid, BankAccount> accounts = await LoadAccountsAsync(context, cancellationToken);
        Dictionary<Guid, string> ruleNames = await LoadRuleNamesAsync(context, payments, cancellationToken);

        // Dates are encrypted, so they're ordered once decrypted
        List<DebtPaymentItem> items = payments
            .OrderByDescending(static x => x.OccurredAt)
            .Select(x => DebtMapper.ToItem(x, AccountName(accounts, x.AccountId) ?? string.Empty,
                RuleName(ruleNames, x.DebtPayment!.RuleId)))
            .ToList();

        IReadOnlyList<DebtBalance> history = await HistoryAsync(context, debt, payments, cancellationToken);

        return new DebtDetail
        {
            Debt = ToContract(debt, accounts, payments),
            Payments = items,
            History = history.Select(DebtMapper.ToContract).ToList()
        };
    }

    public async Task<Debt> AddDebtAsync(SaveDebt debt, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        await EnsureNameIsFreeAsync(context, debt.Name, null, cancellationToken);

        DomainDebt added = new(UserId, debt.Name, debt.Lender, debt.StartingBalance, debt.StartedOn,
            debt.InterestRate, debt.MonthlyPayment);
        added.SetPayOffGoal(debt.PayOffGoal);
        await LinkAsync(context, added, debt.AccountId, cancellationToken);

        context.Debts.Add(added);
        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.Debt, debt.Name, cancellationToken);
        Logger.LogChanged(UserId, "created", "debt", added.Id);

        return await ToContractAsync(context, added, cancellationToken);
    }

    public async Task<Debt> UpdateDebtAsync(Guid id, SaveDebt debt, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainDebt updated = await context.Debts.OwnedBy(UserId).SingleAsync(x => x.Id == id, cancellationToken);

        await EnsureNameIsFreeAsync(context, debt.Name, id, cancellationToken);
        updated.Rename(debt.Name);
        updated.ChangeLender(debt.Lender);
        updated.ChangeStartingBalance(debt.StartingBalance, debt.StartedOn);
        updated.ChangeInterestRate(debt.InterestRate);
        updated.ChangeMonthlyPayment(debt.MonthlyPayment);
        updated.SetPayOffGoal(debt.PayOffGoal);

        await LinkAsync(context, updated, debt.AccountId, cancellationToken);

        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.Debt, debt.Name, cancellationToken);
        Logger.LogChanged(UserId, "updated", "debt", id);

        return await ToContractAsync(context, updated, cancellationToken);
    }

    /// <summary>
    /// Paid off debts count, as their payments still name them.
    /// </summary>
    /// <exception cref="NameTakenException">Another of the user's debts is called <paramref name="name"/>.
    /// </exception>
    private async Task EnsureNameIsFreeAsync(FinanceTrackerContext context, string name, Guid? exceptId,
        CancellationToken cancellationToken)
    {
        List<DomainDebt> existing = await context.Debts.OwnedBy(UserId).AsNoTracking().ToListAsync(cancellationToken);
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

        DomainDebt debt = await context.Debts.OwnedBy(UserId).AsNoTracking()
            .SingleAsync(x => x.Id == id, cancellationToken);

        BankAccount? account = await LoadAccountAsync(context, debt, cancellationToken);
        DebtProgress progress = await ProgressAsync(context, debt, account, cancellationToken);

        return DebtMapper.ToContract(DebtCalculator.Forecast(progress.Owed, debt.InterestRate, monthlyPayment,
            _timeProvider.Today));
    }

    public async Task<Transaction> PayDebtAsync(Guid transactionId, PayDebt payment,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one on an account shared with them with changes allowed, paying one of their own debts. The
        // owner is shared that debt, so they see what it's called
        AccountsInView changeable = await _accountScope.ResolveChangeableAsync(context, cancellationToken);
        BankTransaction transaction = await context.BankTransactions.ChangeableIn(changeable)
            .SingleAsync(x => x.Id == transactionId, cancellationToken);

        if (payment.DebtId is not { } debtId)
        {
            transaction.PayNoDebt(_timeProvider.UtcNow);
        }
        else
        {
            DomainDebt debt = await context.Debts.OwnedBy(UserId).AsNoTracking()
                .SingleAsync(x => x.Id == debtId, cancellationToken);
            transaction.PayDebt(debt, UserId, _timeProvider.UtcNow);
            await AnnotationShares.ShareWithOwnerAsync(context, UserId, transaction, ShareItemType.Debt, debt.Id,
                _timeProvider.UtcNow, cancellationToken);
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

        List<BalanceSnapshot> snapshots = await context.BalanceSnapshots.OwnedBy(UserId).AsNoTracking()
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

    /// <param name="account">The debt's linked account, null when it isn't linked or has gone.</param>
    private async Task<DebtProgress> ProgressAsync(FinanceTrackerContext context, DomainDebt debt,
        BankAccount? account, CancellationToken cancellationToken)
    {
        List<BankTransaction> payments = await LoadPaymentsAsync(context, debt.Id, cancellationToken);

        return DebtCalculator.Progress(debt, account, payments, _timeProvider.LocalTimeZone, _timeProvider.Today);
    }

    private async Task<Debt> ToContractAsync(FinanceTrackerContext context, DomainDebt debt,
        CancellationToken cancellationToken)
    {
        BankAccount? account = await LoadAccountAsync(context, debt, cancellationToken);
        DebtProgress progress = await ProgressAsync(context, debt, account, cancellationToken);

        return DebtMapper.ToContract(debt, account?.DisplayName, progress);
    }

    private Debt ToContract(DomainDebt debt, Dictionary<Guid, BankAccount> accounts, List<BankTransaction> payments)
    {
        BankAccount? account = null;
        if (debt.AccountId is { } accountId)
        {
            account = accounts.GetValueOrDefault(accountId);
        }

        DebtProgress progress = DebtCalculator.Progress(debt, account, payments, _timeProvider.LocalTimeZone,
            _timeProvider.Today);

        return DebtMapper.ToContract(debt, account?.DisplayName, progress);
    }

    private async Task<BankAccount?> LoadAccountAsync(FinanceTrackerContext context, DomainDebt debt,
        CancellationToken cancellationToken)
    {
        if (debt.AccountId is not { } accountId)
        {
            return null;
        }

        return await context.BankAccounts.OwnedBy(UserId).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == accountId, cancellationToken);
    }

    /// <summary>
    /// Every transaction that pays off one of the user's debts, from their own accounts or any shared with them,
    /// whichever view they chose, as the debt is theirs wherever it's paid from. Indexed by debt, so no dates are
    /// needed.
    /// </summary>
    private async Task<List<BankTransaction>> LoadPaymentsAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        List<Guid> debtIds = await OwnDebtIds(context).ToListAsync(cancellationToken);

        return await context.BankTransactions.InView(all).AsNoTracking()
            .Where(x => x.DebtPayment != null && x.DebtPayment.DebtId != null &&
                        debtIds.Contains(x.DebtPayment.DebtId.Value))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The transactions that pay off <paramref name="debtId"/>, from the accounts in
    /// <see cref="LoadPaymentsAsync(FinanceTrackerContext,CancellationToken)"/>.
    /// </summary>
    private async Task<List<BankTransaction>> LoadPaymentsAsync(FinanceTrackerContext context, Guid debtId,
        CancellationToken cancellationToken)
    {
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        return await context.BankTransactions.InView(all).AsNoTracking()
            .Where(x => x.DebtPayment != null && x.DebtPayment.DebtId == debtId)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Guid> OwnDebtIds(FinanceTrackerContext context) =>
        context.Debts.OwnedBy(UserId).Select(static x => x.Id);

    /// <summary>
    /// Their own accounts, and those shared with them, which payments may be from.
    /// </summary>
    private async Task<Dictionary<Guid, BankAccount>> LoadAccountsAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        return await context.BankAccounts.InView(all).AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, cancellationToken);
    }

    /// <summary>
    /// The names of the rules that linked <paramref name="payments"/>, empty when none did.
    /// </summary>
    private async Task<Dictionary<Guid, string>> LoadRuleNamesAsync(FinanceTrackerContext context,
        List<BankTransaction> payments, CancellationToken cancellationToken)
    {
        if (!payments.Exists(static x => x.DebtPayment?.RuleId is not null))
        {
            return [];
        }

        // Names are encrypted, so they're read whole and decrypted
        List<Rule> rules = await context.Rules.OwnedBy(UserId).AsNoTracking().ToListAsync(cancellationToken);
        return rules.ToDictionary(static x => x.Id, static x => x.Name);
    }

    private static string? AccountName(Dictionary<Guid, BankAccount> accounts, Guid accountId) =>
        accounts.GetValueOrDefault(accountId)?.DisplayName;

    private static string? RuleName(Dictionary<Guid, string> ruleNames, Guid? ruleId)
    {
        if (ruleId is not { } id)
        {
            return null;
        }

        return ruleNames.GetValueOrDefault(id);
    }
}
