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

            Dictionary<Guid, string> names = await SharedChosenItems.OwnerNamesAsync(context,
                view.SharedOwnerIds.ToList(), cancellationToken);

            return view.SharedOwners.ToDictionary(static x => x.Key, x => names.GetValueOrDefault(x.Value, string.Empty));
        }

        /// <summary>
        /// The first and last name of the other person each merged joint account in view is joint with, by its shown
        /// copy.
        /// </summary>
        public async Task<Dictionary<Guid, string>> JointWithAsync(FinanceTrackerContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            Guid userId = view.UserId;
            List<Guid> visible = [.. view.OwnAccountIds, .. view.SharedAccountIds];

            var joints = await (
                    from joint in context.JointAccounts
                    join shown in context.BankAccounts on joint.ShownAccountId equals shown.Id
                    join hidden in context.BankAccounts on joint.HiddenAccountId equals hidden.Id
                    where visible.Contains(shown.Id)
                    select new { AccountId = shown.Id, ShownOwner = shown.UserId, HiddenOwner = hidden.UserId })
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            if (joints.Count == 0)
            {
                return [];
            }

            Dictionary<Guid, Guid> others = joints.ToDictionary(static x => x.AccountId,
                x => x.ShownOwner == userId ? x.HiddenOwner : x.ShownOwner);
            Dictionary<Guid, string> names = await SharedChosenItems.OwnerNamesAsync(context,
                others.Values.Distinct().ToList(), cancellationToken);

            return others.ToDictionary(static x => x.Key, x => names.GetValueOrDefault(x.Value, string.Empty));
        }

        /// <summary>
        /// Each account in view by what it's called where the user's told about it, e.g. in a notification. Their
        /// own and merged joint accounts by name, another person's saying whose, e.g. <c>Joint · Sam Taylor's</c>.
        /// </summary>
        public async Task<Dictionary<Guid, string>> LabelsAsync(FinanceTrackerContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            Dictionary<Guid, string> names = await context.BankAccounts.InView(view).AsNoTracking()
                .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);
            Dictionary<Guid, string> sharedBy = await view.SharedByAsync(context, cancellationToken);
            Dictionary<Guid, string> jointWith = await view.JointWithAsync(context, cancellationToken);

            Dictionary<Guid, string> labels = [];
            foreach ((Guid accountId, string name) in names)
            {
                if (sharedBy.TryGetValue(accountId, out string? owner) && !jointWith.ContainsKey(accountId))
                {
                    labels[accountId] = $"{name} · {owner}'s";
                }
                else
                {
                    labels[accountId] = name;
                }
            }

            return labels;
        }
    }
}
