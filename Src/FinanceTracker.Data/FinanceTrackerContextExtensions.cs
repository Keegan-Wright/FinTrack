using FinanceTracker.Data.Models;

namespace FinanceTracker.Data;

public static class FinanceTrackerContextExtensions
{
    extension(FinanceTrackerContext context)
    {
        public IQueryable<FinanceTrackerUser> IsolateToUser(Guid userId) =>
            context.FinanceTrackerUsers.Where(x => x.Id == userId);
    }
}
