using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
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

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IBillsService>]
public class BillsService : ServiceBase<BillsService>, IBillsService
{
    private readonly IBillSyncService _billSyncService;
    private readonly TimeProvider _timeProvider;
    private readonly IAccountScope _accountScope;

    public BillsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<BillsService> logger,
        IBillSyncService billSyncService, TimeProvider timeProvider, IAccountScope accountScope)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _billSyncService = billSyncService;
        _timeProvider = timeProvider;
        _accountScope = accountScope;
    }

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

        List<DomainBill> running = (await context.Bills.OwnedBy(UserId).AsNoTracking()
                .ToListAsync(cancellationToken))
            .Where(x => x.StartsOn <= last && (x.EndedOn is null || x.EndedOn >= first))
            .OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        List<BankTransaction> payments = await LoadPaymentsAsync(context, cancellationToken);
        Dictionary<Guid, string> accountNames = await LoadAccountNamesAsync(context, cancellationToken);
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;

        List<BillStatus> statuses = [];
        List<DomainBill> shown = [];
        foreach (DomainBill bill in running)
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
                    today),
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

    public async IAsyncEnumerable<Bill> GetBillsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Names are encrypted, so they're ordered once decrypted
        List<DomainBill> bills = await context.Bills.OwnedBy(UserId).AsNoTracking().ToListAsync(cancellationToken);
        List<BankTransaction> payments = await LoadPaymentsAsync(context, cancellationToken);
        Dictionary<Guid, string> accountNames = await LoadAccountNamesAsync(context, cancellationToken);
        DateOnly today = _timeProvider.Today;
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;

        foreach (DomainBill bill in bills.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return BillMapper.ToContract(bill, AccountName(accountNames, bill.AccountId), payments, timeZone,
                today);
        }
    }

    public async Task<Bill> GetBillAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBill bill = await context.Bills.OwnedBy(UserId).AsNoTracking()
            .SingleAsync(x => x.Id == id, cancellationToken);

        return await ToContractAsync(context, bill, cancellationToken);
    }

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

    public async Task<Bill> UpdateBillAsync(Guid id, SaveBill bill, CancellationToken cancellationToken)
    {
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            DomainBill updated = await context.Bills.OwnedBy(UserId).SingleAsync(x => x.Id == id, cancellationToken);

            // A seeded bill belongs to its standing order or direct debit's account
            if (updated.SourceId is null)
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

    public Task<Bill> EndBillAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeBillAsync(id, bill => bill.EndByUser(_timeProvider.Today), cancellationToken);

    public Task<Bill> ReopenBillAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeBillAsync(id, static bill => bill.ReopenByUser(), cancellationToken);

    public async Task DeleteBillAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBill bill = await context.Bills.OwnedBy(UserId).SingleAsync(x => x.Id == id, cancellationToken);
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

    public async Task<Transaction> PayBillAsync(Guid transactionId, PayBill payment,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one on an account shared with them with changes allowed, paying one of their own bills. The
        // owner is shared that bill, so they see what it's called
        AccountsInView changeable = await _accountScope.ResolveChangeableAsync(context, cancellationToken);
        BankTransaction transaction = await context.BankTransactions.ChangeableIn(changeable)
            .SingleAsync(x => x.Id == transactionId, cancellationToken);

        if (payment.BillId is not { } billId)
        {
            transaction.PayNoBill(_timeProvider.UtcNow);
        }
        else
        {
            DomainBill bill = await context.Bills.OwnedBy(UserId).AsNoTracking()
                .SingleAsync(x => x.Id == billId, cancellationToken);
            transaction.PayBill(bill, UserId, _timeProvider.UtcNow);
            await AnnotationShares.ShareWithOwnerAsync(context, UserId, transaction, ShareItemType.Bill, bill.Id,
                _timeProvider.UtcNow, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "set the bill paid by", "transaction", transactionId);

        AccountsInView view = await _accountScope.ResolveAllAsync(context, cancellationToken);
        TransactionMapper mapper = await TransactionMapper.LoadAsync(context, view, [transaction],
            _timeProvider.LocalTimeZone, cancellationToken);

        return mapper.ToContract(transaction);
    }

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
        Dictionary<Guid, string> accountNames = await LoadAccountNamesAsync(context, cancellationToken);
        DateOnly today = _timeProvider.Today;

        // Dates are encrypted, so they're ordered once decrypted
        foreach (DomainBillSuggestion suggestion in waiting.OrderByDescending(static x => x.LastPaidOn))
        {
            yield return BillMapper.ToContract(suggestion, AccountName(accountNames, suggestion.AccountId), payments,
                today);
        }
    }

    public async Task<int> GetSuggestionCountAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        return await context.BillSuggestions.OwnedBy(UserId).CountAsync(static x => !x.IsDismissed,
            cancellationToken);
    }

    public async Task<Bill> ConfirmSuggestionAsync(Guid id, CancellationToken cancellationToken)
    {
        DomainBill bill;
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            DomainBillSuggestion suggestion = await context.BillSuggestions.OwnedBy(UserId)
                .SingleAsync(x => x.Id == id && !x.IsDismissed, cancellationToken);

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

    public async Task DismissSuggestionAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBillSuggestion suggestion = await context.BillSuggestions.OwnedBy(UserId)
            .SingleAsync(x => x.Id == id && !x.IsDismissed, cancellationToken);

        suggestion.Dismiss();
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "dismissed", "bill suggestion", id);
    }

    private async Task<Bill> ChangeBillAsync(Guid id, Func<DomainBill, bool> change,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBill bill = await context.Bills.OwnedBy(UserId).SingleAsync(x => x.Id == id, cancellationToken);
        change(bill);
        await context.SaveChangesAsync(cancellationToken);

        return await ToContractAsync(context, bill, cancellationToken);
    }

    /// <summary>
    /// Matches every payment since the bill started, rather than leaving it to the next sync's recent days.
    /// </summary>
    private Task<int> MatchHistoryAsync(DomainBill bill, CancellationToken cancellationToken) =>
        _billSyncService.MatchFromAsync(bill.StartsOn.AddDays(-BillMatcher.WindowDays), cancellationToken);

    private async Task<Bill> ToContractAsync(FinanceTrackerContext context, DomainBill bill,
        CancellationToken cancellationToken)
    {
        // From any account it may be paid from, see LoadPaymentsAsync
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        List<BankTransaction> payments = await context.BankTransactions.InView(all).AsNoTracking()
            .Where(x => x.BillPayment != null && x.BillPayment.BillId == bill.Id)
            .ToListAsync(cancellationToken);

        string accountName = await context.BankAccounts.OwnedBy(UserId)
            .Where(x => x.Id == bill.AccountId)
            .Select(static x => x.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;

        return BillMapper.ToContract(bill, accountName, payments, _timeProvider.LocalTimeZone, _timeProvider.Today);
    }

    /// <summary>
    /// Every transaction that pays one of the user's bills, from their own accounts or any shared with them,
    /// whichever view they chose, as the bill is theirs wherever it's paid from. Indexed by bill, so no dates are
    /// needed.
    /// </summary>
    private async Task<List<BankTransaction>> LoadPaymentsAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        List<Guid> billIds = await context.Bills.OwnedBy(UserId).Select(static x => x.Id)
            .ToListAsync(cancellationToken);

        return await context.BankTransactions.InView(all).AsNoTracking()
            .Where(x => x.BillPayment != null && x.BillPayment.BillId != null &&
                        billIds.Contains(x.BillPayment.BillId.Value))
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadAccountNamesAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken) =>
        await context.BankAccounts.OwnedBy(UserId).AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);

    private static string AccountName(Dictionary<Guid, string> accountNames, Guid accountId) =>
        accountNames.GetValueOrDefault(accountId, string.Empty);

    /// <exception cref="InvalidOperationException">The account doesn't exist or isn't the user's.</exception>
    private async Task EnsureAccountIsTheUsersAsync(FinanceTrackerContext context, Guid accountId,
        CancellationToken cancellationToken)
    {
        bool owned = await context.BankAccounts.OwnedBy(UserId).AnyAsync(x => x.Id == accountId, cancellationToken);
        if (!owned)
        {
            throw new InvalidOperationException("A bill can only be paid from the user's own accounts.");
        }
    }
}
