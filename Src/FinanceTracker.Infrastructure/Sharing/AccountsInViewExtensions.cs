using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Sharing;

public static class AccountsInViewExtensions
{
    extension(AccountsInView view)
    {
        /// <summary>
        /// The first and last name of whoever shares each shared account in view, by account. Their own accounts
        /// aren't in it.
        /// </summary>
        public async Task<Dictionary<Guid, string>> SharedByAsync(FinanceTrackerContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (view.SharedAccountIds.Count == 0)
            {
                return [];
            }

            List<Guid> ownerIds = view.SharedOwnerIds.ToList();
            Dictionary<Guid, string> names = await context.Users.AsNoTracking()
                .Where(x => ownerIds.Contains(x.Id))
                .ToDictionaryAsync(static x => x.Id, static x => $"{x.FirstName} {x.LastName}".Trim(), cancellationToken);

            return view.SharedOwners.ToDictionary(static x => x.Key, x => names.GetValueOrDefault(x.Value, string.Empty));
        }
    }
}
