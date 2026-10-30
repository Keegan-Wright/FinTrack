using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Bill = FinanceTracker.Contracts.Bills.Bill;
using BillSuggestion = FinanceTracker.Contracts.Bills.BillSuggestion;
using DomainBill = FinanceTracker.Domain.Bills.Bill;
using DomainBillPeriod = FinanceTracker.Domain.Bills.BillPeriod;
using DomainBillSource = FinanceTracker.Domain.Bills.BillSource;
using DomainBillSuggestion = FinanceTracker.Domain.Bills.BillSuggestion;

namespace FinanceTracker.Infrastructure.Bills;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IBillsService>]
public class BillsService : ServiceBase<BillsService>, IBillsService
{
    private readonly IBillSyncService _billSyncService;
    private readonly TimeProvider _timeProvider;

    public BillsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<BillsService> logger,
        IBillSyncService billSyncService, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _billSyncService = billSyncService;
        _timeProvider = timeProvider;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Today where the server is.
    /// </summary>
    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private TimeZoneInfo TimeZone => _timeProvider.LocalTimeZone;

    public async Task<BillMonthOverview> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        BudgetMonth target = new(year, month);
        DateOnly first = target.FirstDay;
        DateOnly last = first.AddDays(target.Days - 1);

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<DomainBill> running = (await context.Bills.OwnedBy(UserId).AsNoTracking()
                .ToListAsync(cancellationToken))
            .Where(x => x.StartsOn <= last && (x.EndedOn is null || x.EndedOn >= first))
            .OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        List<BankTransaction> payments = await LoadPaymentsAsync(context, cancellationToken);
        Dictionary<Guid, string> accountNames = await LoadAccountNamesAsync(context, cancellationToken);
        DateOnly today = Today;

        List<BillStatus> statuses = [];
        foreach (DomainBill bill in running)
        {
            IReadOnlyList<DomainBillPeriod> periods =
                BillCalculator.Periods(bill, payments, first, last, TimeZone, today);

            statuses.Add(new BillStatus
            {
                Bill = BillMapper.ToContract(bill, AccountName(accountNames, bill.AccountId), payments, TimeZone,
                    today),
                Periods = periods.Select(BillMapper.ToContract).ToList()
            });
        }

        return new BillMonthOverview
        {
            Month = first,
            IsCurrent = target == BudgetMonth.Of(today),
            Committed = running.Sum(x => BillCalculator.MonthlyCost(x, first)),
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
        DateOnly today = Today;

        foreach (DomainBill bill in bills.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return BillMapper.ToContract(bill, AccountName(accountNames, bill.AccountId), payments, TimeZone,
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
            updated.ChangeAmount(bill.ExpectedAmount, Today);
            updated.ChangeTolerance(bill.TolerancePercent);

            await context.SaveChangesAsync(cancellationToken);

            // The payee or schedule may have changed which payments are its
            await MatchHistoryAsync(updated, cancellationToken);
        }

        return await GetBillAsync(id, cancellationToken);
    }

    public Task<Bill> EndBillAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeBillAsync(id, bill => bill.EndByUser(Today), cancellationToken);

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

        List<BankTransaction> payments = await context.BankTransactions.OwnedBy(UserId)
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
    }

    public async Task PayBillAsync(Guid transactionId, PayBill payment, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        BankTransaction transaction = await context.BankTransactions.OwnedBy(UserId)
            .SingleAsync(x => x.Id == transactionId, cancellationToken);

        if (payment.BillId is not { } billId)
        {
            transaction.PayNoBill(Now);
        }
        else
        {
            DomainBill bill = await context.Bills.OwnedBy(UserId).AsNoTracking()
                .SingleAsync(x => x.Id == billId, cancellationToken);
            transaction.PayBill(bill, Now);
        }

        await context.SaveChangesAsync(cancellationToken);
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
        DateOnly today = Today;

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

            bill = suggestion.ToBill();
            context.Bills.Add(bill);

            // Matched rather than the user's, so matching can still move a payment that fits another bill better
            List<Guid> paymentIds = suggestion.PaymentIds.ToList();
            List<BankTransaction> payments = await context.BankTransactions.OwnedBy(UserId)
                .Where(x => paymentIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
            foreach (BankTransaction payment in payments)
            {
                payment.DecideBillPayment(BillPayment.Matched(bill.Id, Now));
            }

            context.BillSuggestions.Remove(suggestion);
            await context.SaveChangesAsync(cancellationToken);
        }

        await MatchHistoryAsync(bill, cancellationToken);
        return await GetBillAsync(bill.Id, cancellationToken);
    }

    public async Task DismissSuggestionAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainBillSuggestion suggestion = await context.BillSuggestions.OwnedBy(UserId)
            .SingleAsync(x => x.Id == id && !x.IsDismissed, cancellationToken);

        suggestion.Dismiss();
        await context.SaveChangesAsync(cancellationToken);
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
        List<BankTransaction> payments = await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
            .Where(x => x.BillPayment != null && x.BillPayment.BillId == bill.Id)
            .ToListAsync(cancellationToken);

        string accountName = await context.BankAccounts.OwnedBy(UserId)
            .Where(x => x.Id == bill.AccountId)
            .Select(static x => x.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;

        return BillMapper.ToContract(bill, accountName, payments, TimeZone, Today);
    }

    /// <summary>
    /// Every transaction that pays one of the user's bills. Indexed by bill, so no dates are needed.
    /// </summary>
    private async Task<List<BankTransaction>> LoadPaymentsAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken) =>
        await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
            .Where(static x => x.BillPayment != null && x.BillPayment.BillId != null)
            .ToListAsync(cancellationToken);

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
