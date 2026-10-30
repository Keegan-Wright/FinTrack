namespace FinanceTracker.Domain.Bills;

/// <summary>
/// How often a <see cref="Bill"/> is due.
/// </summary>
public enum BillFrequency
{
    Weekly = 1,
    Fortnightly = 2,
    FourWeekly = 3,
    Monthly = 4,
    Quarterly = 5,
    Yearly = 6
}
