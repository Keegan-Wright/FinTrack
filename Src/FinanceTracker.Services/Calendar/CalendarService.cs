using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Response.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Services.Calendar;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<ICalendarService>]
public class CalendarService : ServiceBase<CalendarService>, ICalendarService
{
    public CalendarService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<CalendarService> logger)
        : base(user, financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<CalendarItemsResponse> GetMonthItemsAsync(int month, int year,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        DateTime startDate = new DateTime(year, month, 1).ToUniversalTime();
        DateTime endDate = new DateTime(year, month, daysInMonth).ToUniversalTime();
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        List<IGrouping<DateTime, CalendarTransactionItemResponse>> transactions = await context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!.SelectMany(v => v.Transactions!)))
            .Where(x => x.TransactionTime >= startDate && x.TransactionTime < endDate)
            .Select(x => new CalendarTransactionItemResponse
            {
                Description = x.Description,
                Amount = x.Amount,
                TransactionType = x.TransactionType,
                TransactionTime = x.TransactionTime
            })
            .GroupBy(x => x.TransactionTime)
            .ToListAsync(cancellationToken);

        List<IGrouping<DateTime?, CalendarGoalItemResponse>> goals = await context.IsolateToUser(UserId)
            .Include(x => x.BudgetCategories)
            .SelectMany(x => x.BudgetCategories!)
            .Where(x => x.GoalCompletionDate >= startDate && x.GoalCompletionDate < endDate)
            .Select(x => new CalendarGoalItemResponse { Name = x.Name, GoalCompletionDate = x.GoalCompletionDate })
            .GroupBy(x => x.GoalCompletionDate)
            .ToListAsync(cancellationToken);

        for (int i = 0; i < daysInMonth; i++)
        {
            DateTime date = startDate.AddDays(i);

            CalendarItemsResponse response = new()
            {
                Date = date,
                Transactions = transactions.Where(x => x.Key.Date == date.Date).SelectMany(x => x),
                Goals = goals.Where(x => x.Key == date).SelectMany(x => x)
            };

            yield return response;
        }
    }
}
