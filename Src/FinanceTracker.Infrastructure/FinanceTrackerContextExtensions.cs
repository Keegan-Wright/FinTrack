using FinanceTracker.Domain.Common;

namespace FinanceTracker.Infrastructure;

public static class FinanceTrackerContextExtensions
{
    extension<T>(IQueryable<T> query) where T : class, IUserOwned
    {
        /// <summary>
        /// Restricts an aggregate query to those owned by <paramref name="userId"/>.
        /// </summary>
        public IQueryable<T> OwnedBy(Guid userId) => query.Where(x => x.UserId == userId);
    }
}
