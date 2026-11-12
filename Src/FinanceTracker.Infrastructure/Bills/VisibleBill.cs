using DomainBill = FinanceTracker.Domain.Bills.Bill;

namespace FinanceTracker.Infrastructure.Bills;

/// <summary>
/// A bill a user sees, see <see cref="SharedBills"/>.
/// </summary>
/// <param name="Bill">The bill, its owner's.</param>
/// <param name="SharedBy">Its owner's first and last name when it's someone else's, null when it's the user's.</param>
/// <param name="CanChange">Whether the user can edit, end and reopen it.</param>
internal sealed record VisibleBill(DomainBill Bill, string? SharedBy, bool CanChange)
{
    public bool IsOwn => SharedBy is null;
}
