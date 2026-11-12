using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using DomainSavingsGoal = FinanceTracker.Domain.SavingsGoals.SavingsGoal;

namespace FinanceTracker.Infrastructure.SavingsGoals;

/// <summary>
/// The savings goals a user sees. Their own, those tracking an account shared with them, shared the same way as it,
/// and those another person chose to share with them, see <see cref="ShareItemType.SavingsGoal"/>.
/// </summary>
internal static class SharedGoals
{
    /// <summary>
    /// Every goal <paramref name="userId"/> sees through <paramref name="view"/>, untracked, their own first.
    /// </summary>
    /// <param name="view">Their accounts and those shared with them. A goal tracking one shared with changes allowed
    /// can be changed. Just mine leaves out those chosen for them, as it does accounts.</param>
    public static async Task<List<VisibleItem<DomainSavingsGoal>>> ForAsync(FinanceTrackerContext context,
        Guid userId, AccountsInView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        List<VisibleItem<DomainSavingsGoal>> chosen = await SharedChosenItems.ForAsync(context, context.SavingsGoals,
            userId, ShareItemType.SavingsGoal, view.IsJustMine, cancellationToken);
        // Tracking an account they see that's someone else's, shared as the account is
        IReadOnlyList<Guid> sharedAccounts = view.SharedAccountIds;
        List<DomainSavingsGoal> followingAccounts = await context.SavingsGoals.AsNoTracking()
            .Where(x => x.UserId != userId && x.Source == SavingsGoalSource.Account && x.AccountId != null &&
                        sharedAccounts.Contains(x.AccountId.Value))
            .ToListAsync(cancellationToken);

        return await SharedChosenItems.WithFollowingAsync(context, chosen, followingAccounts,
            static x => x.AccountId!.Value, view, cancellationToken);
    }
}
