using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Infrastructure.Sharing;

namespace FinanceTracker.Infrastructure;

public static class FinanceTrackerContextExtensions
{
    extension<T>(IQueryable<T> query) where T : class, IUserOwned
    {
        /// <summary>
        /// Restricts an aggregate query to those owned by <paramref name="userId"/>. For changes, and for anything
        /// that isn't an account's, which stays with whoever made it.
        /// </summary>
        public IQueryable<T> OwnedBy(Guid userId) => query.Where(x => x.UserId == userId);

        /// <summary>
        /// Restricts to those owned by any of <paramref name="userIds"/>, for naming what shared transactions point
        /// at, such as the tags and bills of whoever shared the account. For reading only.
        /// </summary>
        public IQueryable<T> OwnedByAny(IReadOnlyCollection<Guid> userIds) => query.Where(x => userIds.Contains(x.UserId));
    }

    extension<T>(IQueryable<T> query) where T : AggregateRoot
    {
        /// <summary>
        /// Restricts to those owned by any of <paramref name="userIds"/> or among <paramref name="sharedIds"/>, for
        /// naming what transactions point at, which may be the other person's tag or bill put on one of the user's.
        /// For reading only.
        /// </summary>
        /// <param name="sharedIds">Those shared with the user, see <see cref="SharedItems"/>.</param>
        public IQueryable<T> OwnedByAnyOrShared(IReadOnlyCollection<Guid> userIds, IQueryable<Guid> sharedIds) =>
            query.Where(x => userIds.Contains(x.UserId) || sharedIds.Contains(x.Id));
    }

    extension<T>(IQueryable<T> query) where T : class, IUserOwned, IBelongsToAccount
    {
        /// <summary>
        /// Restricts to what belongs to the accounts the user can change, <paramref name="changeable"/> from
        /// <see cref="IAccountScope.ResolveChangeableAsync"/>: their own, and those shared with changes allowed.
        /// </summary>
        public IQueryable<T> ChangeableIn(AccountsInView changeable) => query.InView(changeable);

        /// <summary>
        /// Restricts to what belongs to the accounts <paramref name="view"/> covers: the user's own, and those shared
        /// with them. For reading only, changes go through <see cref="OwnedBy"/>.
        /// </summary>
        public IQueryable<T> InView(AccountsInView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            Guid userId = view.UserId;
            IReadOnlyList<Guid> shared = view.SharedAccountIds;
            return query.Where(x => x.UserId == userId || shared.Contains(x.AccountId));
        }
    }

    extension(IQueryable<BankAccount> accounts)
    {
        /// <summary>
        /// The accounts <paramref name="view"/> covers the user's own, and those shared with them. For reading only.
        /// </summary>
        public IQueryable<BankAccount> InView(AccountsInView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            Guid userId = view.UserId;
            IReadOnlyList<Guid> shared = view.SharedAccountIds;
            return accounts.Where(x => x.UserId == userId || shared.Contains(x.Id));
        }
    }
}
