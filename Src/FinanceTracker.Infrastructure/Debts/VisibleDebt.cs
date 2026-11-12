using DomainDebt = FinanceTracker.Domain.Debts.Debt;

namespace FinanceTracker.Infrastructure.Debts;

/// <summary>
/// A debt a user sees, see <see cref="SharedDebts"/>.
/// </summary>
/// <param name="Debt">The debt, its owner's.</param>
/// <param name="SharedBy">Its owner's first and last name when it's someone else's, null when it's the user's.</param>
/// <param name="CanChange">Whether the user can edit it.</param>
internal sealed record VisibleDebt(DomainDebt Debt, string? SharedBy, bool CanChange)
{
    public bool IsOwn => SharedBy is null;
}
