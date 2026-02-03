using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Models.Response.Calendar;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Calendar;

public class CalendarService : ServiceBase, ICalendarService
{
    public CalendarService(ClaimsPrincipal user, FinanceTrackerContext financeTrackerContext) : base(user, financeTrackerContext)
    {
    }

    public async IAsyncEnumerable<CalendarItemsResponse> GetMonthItemsAsync(int month, int year, CancellationToken cancellationToken)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var startDate = new DateTime(year, month, 1).ToUniversalTime();
        var endDate = new DateTime(year, month, daysInMonth).ToUniversalTime();
        
        var transactions = await _financeTrackerContext.IsolateToUser(UserId)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers.SelectMany(c => c.Accounts.SelectMany(c => c.Transactions)))
            .Where(x => x.TransactionTime >= startDate && x.TransactionTime < endDate)
            .Select(x => new CalendarTransactionItemResponse(){ Description = x.Description, Amount = x.Amount, TransactionType = x.TransactionType, TransactionTime = x.TransactionTime })
            .GroupBy(x => x.TransactionTime)
            .ToListAsync(cancellationToken: cancellationToken);
        
        var goals = await _financeTrackerContext.IsolateToUser(UserId)
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