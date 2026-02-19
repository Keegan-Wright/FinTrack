using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Response.Calendar;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Calendar;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<ICalendarService>]
public class CalendarService : ServiceBase, ICalendarService
{
    public CalendarService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory) : base(user, financeTrackerContextFactory)
    {
    }

    public async IAsyncEnumerable<CalendarItemsResponse> GetMonthItemsAsync(int month, int year, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var startDate = new DateTime(year, month, 1).ToUniversalTime();
        var endDate = new DateTime(year, month, daysInMonth).ToUniversalTime();
        await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        var transactions = await context.IsolateToUser(UserId)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers.SelectMany(c => c.Accounts.SelectMany(c => c.Transactions)))
            .Where(x => x.TransactionTime >= startDate && x.TransactionTime < endDate)
            .Select(x => new CalendarTransactionItemResponse(){ Description = x.Description, Amount = x.Amount, TransactionType = x.TransactionType, TransactionTime = x.TransactionTime })
            .GroupBy(x => x.TransactionTime)
            .ToListAsync(cancellationToken: cancellationToken);
        
        var goals = await context.IsolateToUser(UserId)
            .Include(x => x.BudgetCategories)
            .SelectMany(x => x.BudgetCategories)
            .Where(x => x.GoalCompletionDate >= startDate && x.GoalCompletionDate < endDate)
            .Select(x => new CalendarGoalItemResponse(){ Name = x.Name, GoalCompletionDate = x.GoalCompletionDate})
            .GroupBy(x => x.GoalCompletionDate)
            .ToListAsync(cancellationToken: cancellationToken);

        for (int i = 0; i < daysInMonth; i++)
        {
            var date = startDate.AddDays(i);

            var response = new CalendarItemsResponse()
            {
                Date = date,
                Transactions = transactions.Where(x => x.Key.Date == date.Date).SelectMany(x => x),
                Goals = goals.Where(x => x.Key == date).SelectMany(x => x),
                
            };

            yield return response;
        }
    }
}