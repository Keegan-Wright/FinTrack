using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Calendar;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ICalendarService>]
public class CalendarService : ServiceBase<CalendarService>, ICalendarService
{
    private readonly TimeProvider _timeProvider;

    public CalendarService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<CalendarService> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    public async IAsyncEnumerable<CalendarItems> GetMonthItemsAsync(int month, int year,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        DateTime startDate = new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime endDate = startDate.AddMonths(1);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Transaction timestamps / goal dates are encrypted at rest, so the month's transactions are found by their day
        // blind index, then filtered exactly once decrypted.
        // The calendar is income and spending, so transfers are left out like the dashboard's totals
        List<CalendarTransactionItem> transactionItems = (await context.BankTransactions.OwnedBy(UserId)
                .WhereEncryptedDayBetween(x => x.OccurredAt, startDate, endDate, context)
                .Where(static x => x.Transfer == null || x.Transfer.Decision != TransferDecision.Transfer)
                .Select(static x => new { x.Description, x.Amount, x.Type, x.OccurredAt })
                .ToListAsync(cancellationToken))
            .Select(static x => new CalendarTransactionItem
            {
                Description = x.Description,
                Amount = x.Amount,
                TransactionType = x.Type.ToString(),
                TransactionTime = x.OccurredAt
            })
            .ToList();

        List<IGrouping<DateTime, CalendarTransactionItem>> transactions = transactionItems
            .Where(x => x.TransactionTime >= startDate && x.TransactionTime < endDate)
            .GroupBy(static x => x.TransactionTime.Date)
            .ToList();

        List<CalendarGoalItem> goalItems = await context.SavingsGoals.OwnedBy(UserId)
            .Select(x => new CalendarGoalItem { Name = x.Name, GoalCompletionDate = x.TargetDate })
            .ToListAsync(cancellationToken);

        List<IGrouping<DateTime, CalendarGoalItem>> goals = goalItems
            .Where(x => x.GoalCompletionDate is { } goalDate && goalDate >= startDate && goalDate < endDate)
            .GroupBy(static x => x.GoalCompletionDate!.Value.Date)
            .ToList();

        Dictionary<DateOnly, List<CalendarBillItem>> bills = await LoadBillsAsync(context,
            DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDate).AddDays(-1), cancellationToken);

        for (int i = 0; i < daysInMonth; i++)
        {
            DateTime date = startDate.AddDays(i);

            CalendarItems response = new()
            {
                Date = date,
                Transactions = transactions.Where(x => x.Key == date).SelectMany(static x => x),
                Goals = goals.Where(x => x.Key == date).SelectMany(static x => x),
                Bills = bills.GetValueOrDefault(DateOnly.FromDateTime(date), [])
            };

            yield return response;
        }
    }

    /// <summary>
    /// Each bill's periods due from <paramref name="first"/> to <paramref name="last"/>, by the day they're due.
    /// </summary>
    private async Task<Dictionary<DateOnly, List<CalendarBillItem>>> LoadBillsAsync(FinanceTrackerContext context,
        DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        List<Bill> bills = await context.Bills.OwnedBy(UserId).AsNoTracking().ToListAsync(cancellationToken);
        List<BankTransaction> payments = await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
            .Where(static x => x.BillPayment != null && x.BillPayment.BillId != null)
            .ToListAsync(cancellationToken);

        DateOnly today = _timeProvider.Today;
        Dictionary<DateOnly, List<CalendarBillItem>> byDay = [];

        foreach (Bill bill in bills)
        {
            foreach (BillPeriod period in BillCalculator.Periods(bill, payments, first, last,
                         _timeProvider.LocalTimeZone, today))
            {
                if (!byDay.TryGetValue(period.Due, out List<CalendarBillItem>? items))
                {
                    items = [];
                    byDay.Add(period.Due, items);
                }

                items.Add(ToItem(bill, period));
            }
        }

        return byDay;
    }

    private static CalendarBillItem ToItem(Bill bill, BillPeriod period)
    {
        decimal amount = period.Expected;
        if (period.PaymentIds.Count > 0)
        {
            amount = period.Paid;
        }

        return new CalendarBillItem
        {
            BillId = bill.Id,
            Name = bill.Name,
            Amount = amount,
            State = Enum.Parse<Contracts.Bills.BillState>(period.State.ToString())
        };
    }
}
