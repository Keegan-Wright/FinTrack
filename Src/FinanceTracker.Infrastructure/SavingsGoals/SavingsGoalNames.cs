using FinanceTracker.Contracts;
using FinanceTracker.Domain.SavingsGoals;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.SavingsGoals;

/// <summary>
/// Keeps a user's savings goal names apart, whether the goal is made on the savings goals page or for a calendar
/// event.
/// </summary>
internal static class SavingsGoalNames
{
    /// <param name="context">The context to work through.</param>
    /// <param name="userId">The user.</param>
    /// <param name="name">The goal's name.</param>
    /// <param name="exceptId">The goal being renamed, which can keep its own name in a different case.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <exception cref="NameTakenException">Another of the user's goals is called <paramref name="name"/>.
    /// </exception>
    public static async Task EnsureFreeAsync(FinanceTrackerContext context, Guid userId, string name, Guid? exceptId,
        CancellationToken cancellationToken)
    {
        List<SavingsGoal> existing = await context.SavingsGoals.OwnedBy(userId).AsNoTracking()
            .ToListAsync(cancellationToken);
        NameCheck.EnsureFree(existing.Select(static x => (x.Id, x.Name)), name, exceptId,
            NameTakenException.SavingsGoal);
    }
}
