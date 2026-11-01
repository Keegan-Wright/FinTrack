namespace FinanceTracker.Domain.Bills;

/// <summary>
/// How one period of a <see cref="Bill"/> stands, see <see cref="BillCalculator"/>.
/// </summary>
public enum BillState
{
    /// <summary>
    /// Not paid yet, and not past its due date by more than <see cref="BillCalculator.LateAfterDays"/>.
    /// </summary>
    Upcoming = 1,

    /// <summary>
    /// Paid within the expected amount's tolerance.
    /// </summary>
    Paid = 2,

    /// <summary>
    /// Past its due date by more than <see cref="BillCalculator.LateAfterDays"/> with no payment.
    /// </summary>
    Late = 3,

    /// <summary>
    /// Paid, but outside the expected amount's tolerance.
    /// </summary>
    Changed = 4
}
