using System.Linq.Expressions;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;

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

        /// <summary>
        /// The one matching <paramref name="predicate"/>, for looking up what a request names by its id.
        /// </summary>
        /// <param name="what">What it is, as it reads in a sentence, e.g. <c>tag</c>.</param>
        /// <exception cref="NotFoundException">None does, such as one that's gone or isn't the user's to see.
        /// </exception>
        public async Task<T> SingleOrNotFoundAsync(Expression<Func<T, bool>> predicate, string what,
            CancellationToken cancellationToken) =>
            await query.SingleOrDefaultAsync(predicate, cancellationToken)
            ?? throw new NotFoundException($"No {what} the user can see or change was found.");
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
        /// Restricts to what belongs to the accounts the user can change in <paramref name="view"/>: their own, and
        /// those shared with changes allowed. Whichever view is passed, a shared account that only lets them see it is
        /// left out.
        /// </summary>
        public IQueryable<T> ChangeableIn(AccountsInView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            Guid userId = view.UserId;
            List<Guid> changeable = view.ChangeableSharedAccountIds();
            List<Guid> hidden = view.HiddenAccountIds.ToList();

            return query.Where(x =>
                (x.UserId == userId && !hidden.Contains(x.AccountId)) || changeable.Contains(x.AccountId));
        }

        /// <summary>
        /// Restricts to what belongs to the accounts <paramref name="view"/> covers: the user's own, their copies of
        /// joint accounts aside, and those shared with them. For reading only, changes go through
        /// <see cref="OwnedBy"/>.
        /// </summary>
        public IQueryable<T> InView(AccountsInView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            Guid userId = view.UserId;
            IReadOnlyList<Guid> shared = view.SharedAccountIds;
            List<Guid> hidden = view.HiddenAccountIds.ToList();
            return query.Where(x =>
                (x.UserId == userId && !hidden.Contains(x.AccountId)) || shared.Contains(x.AccountId));
        }
    }

    extension(IQueryable<BankTransaction> query)
    {
        /// <summary>
        /// <see cref="ChangeableIn{T}"/> for transactions, with the hidden copies' that fill a gap in a joint account
        /// the user can change.
        /// </summary>
        public IQueryable<BankTransaction> ChangeableIn(AccountsInView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            Guid userId = view.UserId;
            List<Guid> changeable = view.ChangeableSharedAccountIds();
            List<Guid> hidden = view.HiddenAccountIds.ToList();
            List<Guid> filling = view.FillingAccounts
                .Where(x => view.OwnAccountIds.Contains(x.Value) || view.ChangeableAccountIds.Contains(x.Value))
                .Select(static x => x.Key)
                .ToList();
            return query.Where(x =>
                (x.UserId == userId && !hidden.Contains(x.AccountId)) || changeable.Contains(x.AccountId) ||
                (x.FillsJointGap && filling.Contains(x.AccountId)));
        }

        /// <summary>
        /// <see cref="InView{T}"/> for transactions, with the hidden copies' that fill a joint account's gap, see
        /// <see cref="BankTransaction.FillsJointGap"/>. They show as the shown copy's, see
        /// <see cref="AccountsInView.ShownAs"/>.
        /// </summary>
        public IQueryable<BankTransaction> InView(AccountsInView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            Guid userId = view.UserId;
            IReadOnlyList<Guid> shared = view.SharedAccountIds;
            List<Guid> hidden = view.HiddenAccountIds.ToList();
            IReadOnlyList<Guid> filling = view.FillingAccountIds;
            return query.Where(x =>
                (x.UserId == userId && !hidden.Contains(x.AccountId)) || shared.Contains(x.AccountId) ||
                (x.FillsJointGap && filling.Contains(x.AccountId)));
        }
    }

    extension(IQueryable<BankAccount> accounts)
    {
        /// <summary>
        /// The accounts <paramref name="view"/> covers: the user's own, their copies of joint accounts aside, and those
        /// shared with them. For reading only.
        /// </summary>
        public IQueryable<BankAccount> InView(AccountsInView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            Guid userId = view.UserId;
            IReadOnlyList<Guid> shared = view.SharedAccountIds;
            List<Guid> hidden = view.HiddenAccountIds.ToList();
            return accounts.Where(x => (x.UserId == userId && !hidden.Contains(x.Id)) || shared.Contains(x.Id));
        }
    }
}
