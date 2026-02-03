using FinanceTracker.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Data;

public static class FinanceTrackerContextExtensions
{
    extension(FinanceTrackerContext context)
    {
        public IQueryable<FinanceTrackerUser> IsolateToUser(Guid userId)
        {
            return context.FinanceTrackerUsers.Where(x => x.Id == userId);
        }
    }
}