using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Calendar;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ICalendarService>]
public class CalendarService : ServiceBase<CalendarService>, ICalendarService
{
    public CalendarService(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<CalendarService> logger)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<CalendarItems> GetMonthItemsAsync(int month, int year,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        DateTime startDate = new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime endDate = startDate.AddMonths(1);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // IMPORTANT:
        // Transaction timestamps / goal dates are encrypted at rest, so we cannot filter by date in SQL.
        // Materialize first, then filter in-memory on decrypted values.
        // The calendar is income and spending, so transfers are left out like the dashboard's totals
        List<CalendarTransactionItem> transactionItems = (await context.BankTransactions.OwnedBy(UserId)
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

        List<CalendarGoalItem> goalItems = await context.BudgetCategories.OwnedBy(UserId)
            .Select(x => new CalendarGoalItem { Name = x.Name, GoalCompletionDate = x.GoalCompletionDate })
            .ToListAsync(cancellationToken);

        List<IGrouping<DateTime, CalendarGoalItem>> goals = goalItems
            .Where(x => x.GoalCompletionDate is { } goalDate && goalDate >= startDate && goalDate < endDate)
            .GroupBy(static x => x.GoalCompletionDate!.Value.Date)
            .ToList();

        for (int i = 0; i < daysInMonth; i++)
        {
            DateTime date = startDate.AddDays(i);

            CalendarItems response = new()
            {
                Date = date,
                Transactions = transactions.Where(x => x.Key == date).SelectMany(static x => x),
                Goals = goals.Where(x => x.Key == date).SelectMany(static x => x)
            };

            yield return response;
        }
    }
}
