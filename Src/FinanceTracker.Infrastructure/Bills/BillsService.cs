using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Common;
using FinanceTracker.Infrastructure.PayPeriods;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Bill = FinanceTracker.Contracts.Bills.Bill;
using BillSuggestion = FinanceTracker.Contracts.Bills.BillSuggestion;
using DomainBill = FinanceTracker.Domain.Bills.Bill;
using DomainBillPeriod = FinanceTracker.Domain.Bills.BillPeriod;
using DomainBillSource = FinanceTracker.Domain.Bills.BillSource;
using DomainBillSuggestion = FinanceTracker.Domain.Bills.BillSuggestion;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Infrastructure.Bills;

/// <inheritdoc cref="IBillsService"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IBillsService>]
public class BillsService : ServiceBase<BillsService>, IBillsService
{
    private readonly IBillSyncService _billSyncService;
    private readonly TimeProvider _timeProvider;
    private readonly IAccountScope _accountScope;

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="billSyncService">Syncs the bills.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="accountScope">Which accounts the user can see and change.</param>
    public BillsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<BillsService> logger,
        IBillSyncService billSyncService, TimeProvider timeProvider, IAccountScope accountScope)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _billSyncService = billSyncService;
        _timeProvider = timeProvider;
        _accountScope = accountScope;
    }

    /// <inheritdoc/>
    public async Task<BillMonthOverview> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        BudgetMonth target = new(year, month);

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // The period named after the month, from pay to pay once the user's pay is picked up
        DateOnly today = _timeProvider.Today;
        PayCalendar calendar = await PayCalendars.ForUserAsync(context, UserId, today, cancellationToken);
        PayPeriod period = calendar.Named(year, month);
        PayPeriod current = calendar.Containing(today);
        DateOnly first = period.Start;
        DateOnly last = period.End;

        // Their own and those shared with them, an account's bills being shared with it
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        List<VisibleItem<DomainBill>> running = (await SharedBills.ForAsync(context, UserId, all, cancellationToken))
            .Where(x => x.Item.StartsOn <= last && (x.Item.EndedOn is null || x.Item.EndedOn >= first))
            .OrderBy(static x => x.Item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        List<BankTransaction> payments = await SharedBills.PaymentsAsync(context, running, cancellationToken);
        Dictionary<Guid, string> accountNames = await LoadAccountNamesAsync(context, all, cancellationToken);
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;

        List<BillStatus> statuses = [];
        List<DomainBill> shown = [];
        foreach ((DomainBill bill, string? sharedBy, bool canChange) in running)
        {
            IReadOnlyList<DomainBillPeriod> periods =
                BillCalculator.Periods(bill, payments, first, last, timeZone, today);

            // One that ended before it was next due isn't part of the month, the ended bills list it
            if (bill.HasEnded && periods.Count == 0)
            {
                continue;
            }

            shown.Add(bill);
            statuses.Add(new BillStatus
            {
                Bill = BillMapper.ToContract(bill, AccountName(accountNames, bill.AccountId), payments, timeZone,
                    today, sharedBy, canChange),
                Periods = periods.Select(BillMapper.ToContract).ToList()
            });
        }

        return new BillMonthOverview
        {
            Month = target.FirstDay,
            Start = first,
            End = last,
            IsCurrent = current.Year == year && current.Month == month,
            Committed = shown.Sum(x => BillCalculator.MonthlyCost(x, first)),
            Due = statuses.SelectMany(static x => x.Periods).Sum(static x => x.Expected),
            Paid = statuses.SelectMany(static x => x.Periods).Sum(static x => x.Paid),
            Bills = statuses
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Bill> GetBillsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own and those shared with them. Names are encrypted, so they're ordered once decrypted
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        List<VisibleItem<DomainBill>> bills = await SharedBills.ForAsync(context, UserId, all, cancellationToken);
        List<BankTransaction> payments = await SharedBills.PaymentsAsync(context, bills, cancellationToken);
        Dictionary<Guid, string> accountNames = await LoadAccountNamesAsync(context, all, cancellationToken);

        DateOnly today = _timeProvider.Today;
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;

        foreach ((DomainBill bill, string? sharedBy, bool canChange) in bills
                     .OrderBy(static x => x.Item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return BillMapper.ToContract(bill, AccountName(accountNames, bill.AccountId), payments, timeZone,
                today, sharedBy, canChange);
        }
    }

    /// <inheritdoc/>
    public async Task<Bill> GetBillAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        VisibleItem<DomainBill> bill = await VisibleAsync(context, id, mustChange: false, cancellationToken);
        return await ToContractAsync(context, bill, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Bill> AddBillAsync(SaveBill bill, CancellationToken cancellationToken)
    {
        Guid id;
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await EnsureAccountIsTheUsersAsync(context, bill.AccountId, cancellationToken);

            DomainBill added = new(UserId, bill.AccountId, bill.Name, bill.Payee,
                BillMapper.ToDomain(bill.Frequency), bill.ExpectedAmount, bill.NextDue, DomainBillSource.Manual);
            added.ChangeTolerance(bill.TolerancePercent);

            context.Bills.Add(added);
            await context.SaveChangesAsync(cancellationToken);
            Logger.LogChanged(UserId, "created", "bill", added.Id);
            id = added.Id;

            await MatchHistoryAsync(added, cancellationToken);
        }

        return await GetBillAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Bill> UpdateBillAsync(Guid id, SaveBill bill, CancellationToken cancellationToken)
    {
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            // Their own, or one shared with them with changes allowed, which stays on its owner's account
            VisibleItem<DomainBill> visible = await VisibleAsync(context, id, mustChange: true, cancellationToken);
            DomainBill updated = await context.Bills.SingleAsync(x => x.Id == id, cancellationToken);

            if (!visible.IsOwn && bill.AccountId != updated.AccountId)
            {
                throw new DomainRuleException("Only its owner can move a shared bill to another account.");
            }

            // A seeded bill belongs to its standing order or direct debit's account
            if (visible.IsOwn && updated.SourceId is null)
            {
                await EnsureAccountIsTheUsersAsync(context, bill.AccountId, cancellationToken);
                updated.MoveToAccount(bill.AccountId);
            }

            updated.Rename(bill.Name);
            updated.ChangePayee(bill.Payee);
            updated.Reschedule(BillMapper.ToDomain(bill.Frequency), bill.NextDue);
            updated.ChangeAmount(bill.ExpectedAmount, _timeProvider.Today);
            updated.ChangeTolerance(bill.TolerancePercent);

            await context.SaveChangesAsync(cancellationToken);
            Logger.LogChanged(UserId, "updated", "bill", id);

            // The payee or schedule may have changed which payments are its
            await MatchHistoryAsync(updated, cancellationToken);
        }

        return await GetBillAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Bill> EndBillAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeBillAsync(id, bill => bill.EndByUser(_timeProvider.Today), cancellationToken);

    /// <inheritdoc/>
    public Task<Bill> ReopenBillAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeBillAsync(id, static bill => bill.ReopenByUser(), cancellationToken);

    /// <inheritdoc/>
    public async Task DeleteBillAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBill bill = await context.Bills.OwnedBy(UserId)
            .SingleOrNotFoundAsync(x => x.Id == id, "bill", cancellationToken);
        if (bill.SourceId is not null)
        {
            throw new NotSupportedException(
                "A bill from a standing order or direct debit can only be ended, the next sync would add it again.");
        }

        // Whoever's transaction pays it, they may have let the user say theirs does
        List<BankTransaction> payments = await context.BankTransactions
            .Where(x => x.BillPayment != null && x.BillPayment.BillId == id)
            .ToListAsync(cancellationToken);
        foreach (BankTransaction payment in payments)
        {
            payment.RemoveDeletedBill(id);
        }

        // Action ids are inside each rule's JSON, so every rule is checked
        List<Rule> rules = await context.Rules.OwnedBy(UserId).ToListAsync(cancellationToken);
        foreach (Rule rule in rules)
        {
            rule.RemoveBill(id);
        }

        context.Bills.Remove(bill);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "deleted", "bill", id);
    }

    /// <inheritdoc/>
    public async Task<Transaction> PayBillAsync(Guid transactionId, PayBill payment,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one on an account shared with them with changes allowed, paying one of their own bills. The
        // owner is shared that bill, so they see what it's called
        AccountsInView changeable = await _accountScope.ResolveChangeableAsync(context, cancellationToken);
        BankTransaction transaction = await context.BankTransactions.ChangeableIn(changeable)
            .SingleOrNotFoundAsync(x => x.Id == transactionId, "transaction", cancellationToken);

        if (payment.BillId is not { } billId)
        {
            transaction.PayNoBill(_timeProvider.UtcNow);
        }
        else
        {
            // Their own, or one shared with them. Their own is shared with the transaction's owner so they see its name
            VisibleItem<DomainBill> bill = await VisibleAsync(context, billId, mustChange: false, cancellationToken);
            transaction.PayBill(bill.Item, UserId, _timeProvider.UtcNow, sharedWithThem: !bill.IsOwn);

            if (bill.IsOwn)
            {
                await AnnotationShares.ShareWithOwnerAsync(context, UserId, transaction, ShareItemType.Bill, billId,
                    _timeProvider.UtcNow, cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "set the bill paid by", "transaction", transactionId);

        AccountsInView view = await _accountScope.ResolveAllAsync(context, cancellationToken);
        TransactionMapper mapper = await TransactionMapper.LoadAsync(context, view, [transaction],
            _timeProvider.LocalTimeZone, cancellationToken);

        return mapper.ToContract(transaction);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BillSuggestion> GetSuggestionsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<DomainBillSuggestion> waiting = await context.BillSuggestions.OwnedBy(UserId).AsNoTracking()
            .Where(static x => !x.IsDismissed)
            .ToListAsync(cancellationToken);

        List<Guid> paymentIds = waiting.SelectMany(static x => x.PaymentIds).Distinct().ToList();
        Dictionary<Guid, BankTransaction> payments = await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
            .Where(x => paymentIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, cancellationToken);

        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);

        Dictionary<Guid, string> accountNames = await LoadAccountNamesAsync(context, all, cancellationToken);
        DateOnly today = _timeProvider.Today;

        // Dates are encrypted, so they're ordered once decrypted
        foreach (DomainBillSuggestion suggestion in waiting.OrderByDescending(static x => x.LastPaidOn))
        {
            yield return BillMapper.ToContract(suggestion, AccountName(accountNames, suggestion.AccountId), payments,
                today);
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetSuggestionCountAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        return await context.BillSuggestions.OwnedBy(UserId).CountAsync(static x => !x.IsDismissed,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Bill> ConfirmSuggestionAsync(Guid id, CancellationToken cancellationToken)
    {
        DomainBill bill;
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            DomainBillSuggestion suggestion = await context.BillSuggestions.OwnedBy(UserId)
                .SingleOrNotFoundAsync(x => x.Id == id && !x.IsDismissed, "bill suggestion", cancellationToken);

            List<Guid> paymentIds = suggestion.PaymentIds.ToList();
            List<BankTransaction> payments = await context.BankTransactions.OwnedBy(UserId)
                .Where(x => paymentIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            await EnsureNotCoveredAsync(context, suggestion, payments, cancellationToken);

            bill = suggestion.ToBill();
            context.Bills.Add(bill);

            // Matched rather than the user's, so matching can still move a payment that fits another bill better
            foreach (BankTransaction payment in payments)
            {
                payment.DecideBillPayment(BillPayment.Matched(bill.Id, _timeProvider.UtcNow));
            }

            context.BillSuggestions.Remove(suggestion);
            await context.SaveChangesAsync(cancellationToken);
            Logger.LogChanged(UserId, "confirmed a suggestion as", "bill", bill.Id);
        }

        await MatchHistoryAsync(bill, cancellationToken);
        return await GetBillAsync(bill.Id, cancellationToken);
    }

    /// <summary>
    /// A bill added since the suggestion was made may already cover its payments. The next sync would drop it, so
    /// it's dropped now rather than made a second bill for the same payments.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="suggestion">The suggestion.</param>
    /// <param name="payments">The payments.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <exception cref="AlreadyABillException">A running bill on its account is paid by its latest payment.
    /// </exception>
    private async Task EnsureNotCoveredAsync(FinanceTrackerContext context, DomainBillSuggestion suggestion,
        List<BankTransaction> payments, CancellationToken cancellationToken)
    {
        BankTransaction? latest = payments.MaxBy(static x => x.OccurredAt);
        if (latest is null)
        {
            return;
        }

        List<DomainBill> bills = await context.Bills.OwnedBy(UserId).AsNoTracking()
            .Where(x => x.AccountId == suggestion.AccountId)
            .ToListAsync(cancellationToken);
        if (SubscriptionDetector.BillCovering(bills, latest) is not { } covering)
        {
            return;
        }

        context.BillSuggestions.Remove(suggestion);
        await context.SaveChangesAsync(cancellationToken);
        throw new AlreadyABillException(covering.Name);
    }

    /// <inheritdoc/>
    public async Task DismissSuggestionAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBillSuggestion suggestion = await context.BillSuggestions.OwnedBy(UserId)
            .SingleOrNotFoundAsync(x => x.Id == id && !x.IsDismissed, "bill suggestion", cancellationToken);

        suggestion.Dismiss();
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "dismissed", "bill suggestion", id);
    }

    /// <summary>
    /// Makes <paramref name="change"/> to a bill the user can change, saves it and returns it as it is now.
    /// </summary>
    /// <param name="id">The bill.</param>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<Bill> ChangeBillAsync(Guid id, Func<DomainBill, bool> change,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        VisibleItem<DomainBill> visible = await VisibleAsync(context, id, mustChange: true, cancellationToken);
        DomainBill bill = await context.Bills.SingleAsync(x => x.Id == id, cancellationToken);
        change(bill);
        await context.SaveChangesAsync(cancellationToken);

        return await ToContractAsync(context, visible with { Item = bill }, cancellationToken);
    }

    /// <summary>
    /// Matches every payment since the bill started, rather than leaving it to the next sync's recent days. As its
    /// owner, over their transactions, whoever changed it.
    /// </summary>
    /// <param name="bill">The bill.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private Task<int> MatchHistoryAsync(DomainBill bill, CancellationToken cancellationToken) =>
        _billSyncService.MatchFromForAsync(bill.UserId, bill.StartsOn.AddDays(-BillMatcher.WindowDays),
            cancellationToken);

    /// <summary>
    /// Bill <paramref name="id"/>, the user's own or one shared with them.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="id">Its id.</param>
    /// <param name="mustChange">Whether they must be allowed to change it.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <exception cref="InvalidOperationException">They can't see it, or can't change it.</exception>
    private async Task<VisibleItem<DomainBill>> VisibleAsync(FinanceTrackerContext context, Guid id, bool mustChange,
        CancellationToken cancellationToken)
    {
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        VisibleItem<DomainBill>? bill = (await SharedBills.ForAsync(context, UserId, all, cancellationToken))
            .SingleOrDefault(x => x.Item.Id == id);

        if (bill is null || (mustChange && !bill.CanChange))
        {
            throw new NotFoundException($"No bill {id} the user can see or change was found.");
        }

        return bill;
    }

    /// <summary>
    /// A bill as the API sends it, with its payments and account's name.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="visible">The bill, as the user sees it.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<Bill> ToContractAsync(FinanceTrackerContext context, VisibleItem<DomainBill> visible,
        CancellationToken cancellationToken)
    {
        // From any account it may be paid from, see SharedBills.PaymentsAsync
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        List<BankTransaction> payments = await SharedBills.PaymentsAsync(context, [visible], cancellationToken);
        Dictionary<Guid, string> accountNames = await LoadAccountNamesAsync(context, all, cancellationToken);

        return BillMapper.ToContract(visible.Item, AccountName(accountNames, visible.Item.AccountId), payments,
            _timeProvider.LocalTimeZone, _timeProvider.Today, visible.SharedBy, visible.CanChange);
    }

    /// <summary>
    /// The names of the accounts the user sees, a shared bill's account among them.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="all">Every account the user sees.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private static async Task<Dictionary<Guid, string>> LoadAccountNamesAsync(FinanceTrackerContext context,
        AccountsInView all, CancellationToken cancellationToken) =>
        await context.BankAccounts.InView(all).AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);

    /// <summary>
    /// The account's name, or that it's private for a shared bill paid from an account the user can't see.
    /// </summary>
    /// <param name="accountNames">The accounts' names, by id.</param>
    /// <param name="accountId">The account.</param>
    private static string AccountName(Dictionary<Guid, string> accountNames, Guid accountId) =>
        accountNames.GetValueOrDefault(accountId, Rules.SharedRules.PrivateAccountName);

    /// <param name="context">The context to work through.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <exception cref="InvalidOperationException">The account doesn't exist or isn't the user's.</exception>
    private async Task EnsureAccountIsTheUsersAsync(FinanceTrackerContext context, Guid accountId,
        CancellationToken cancellationToken)
    {
        bool owned = await context.BankAccounts.OwnedBy(UserId).AnyAsync(x => x.Id == accountId, cancellationToken);
        if (!owned)
        {
            throw new DomainRuleException("A bill can only be paid from the user's own accounts.");
        }
    }
}
