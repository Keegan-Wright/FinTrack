using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Calendar;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<ICalendarService>]
public class CalendarService : ServiceBase<CalendarService>, ICalendarService
{
    public CalendarService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<CalendarService> logger)
        : base(user, financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<CalendarItems> GetMonthItemsAsync(int month, int year,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        DateTime startDate = new DateTime(year, month, 1).ToUniversalTime();
        DateTime endDate = new DateTime(year, month, daysInMonth).ToUniversalTime();
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // IMPORTANT:
        // Transaction timestamps / goal dates are encrypted at rest, so we cannot filter by date in SQL.
        // Materialize first, then filter in-memory on decrypted values.
        List<CalendarTransactionItem> transactionItems = await context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!.SelectMany(v => v.Transactions!)))
            .Select(x => new CalendarTransactionItem
            {
                Description = x.Description,
                Amount = x.Amount,
                TransactionType = x.TransactionType,
                TransactionTime = x.TransactionTime
            })
            .ToListAsync(cancellationToken);

        List<IGrouping<DateTime, CalendarTransactionItem>> transactions = transactionItems
            .Where(x => x.TransactionTime >= startDate && x.TransactionTime < endDate)
            .GroupBy(x => x.TransactionTime)
            .ToList();

        List<CalendarGoalItem> goalItems = await context.IsolateToUser(UserId)
            .Include(x => x.BudgetCategories)
            .SelectMany(x => x.BudgetCategories!)
            .Select(x => new CalendarGoalItem { Name = x.Name, GoalCompletionDate = x.GoalCompletionDate })
            .ToListAsync(cancellationToken);

        List<IGrouping<DateTime?, CalendarGoalItem>> goals = goalItems
            .Where(x => x.GoalCompletionDate >= startDate && x.GoalCompletionDate < endDate)
            .GroupBy(x => x.GoalCompletionDate)
            .ToList();

        for (int i = 0; i < daysInMonth; i++)
        {
            DateTime date = startDate.AddDays(i);

            CalendarItems response = new()
            {
                Date = date,
                Transactions = transactions.Where(x => x.Key.Date == date.Date).SelectMany(x => x),
                Goals = goals.Where(x => x.Key == date).SelectMany(x => x)
            };

            yield return response;
        }
    }
}
