using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Domain.Bills;

/// <summary>
/// Reads a <see cref="StandingOrder.Frequency"/> as a <see cref="BillFrequency"/>. Banks send either a plain word,
/// e.g. <c>Monthly</c>, or an Open Banking code, e.g. <c>IntrvlMnthDay:01:15</c> for monthly on the 15th.
/// </summary>
public static class StandingOrderFrequency
{
    /// <returns>Null when it's one a bill can't follow, e.g. every working day, or isn't recognised.</returns>
    public static BillFrequency? Parse(string? frequency)
    {
        if (string.IsNullOrWhiteSpace(frequency))
        {
            return null;
        }

        string[] parts = frequency.Trim().Split(':');
        string kind = parts[0].ToUpperInvariant();

        switch (kind)
        {
            case "WEEKLY":
                return BillFrequency.Weekly;
            case "FORTNIGHTLY":
                return BillFrequency.Fortnightly;
            case "MONTHLY":
            case "WKINMNTHDAY":
                return BillFrequency.Monthly;
            case "QUARTERLY":
            case "QTRDAY":
                return BillFrequency.Quarterly;
            case "YEARLY":
            case "ANNUAL":
            case "ANNUALLY":
                return BillFrequency.Yearly;
            case "INTRVLWKDAY":
                return Interval(parts) switch
                {
                    1 => BillFrequency.Weekly,
                    2 => BillFrequency.Fortnightly,
                    4 => BillFrequency.FourWeekly,
                    _ => null
                };
            case "INTRVLMNTHDAY":
                return Interval(parts) switch
                {
                    1 => BillFrequency.Monthly,
                    3 => BillFrequency.Quarterly,
                    12 => BillFrequency.Yearly,
                    _ => null
                };
            default:
                return null;
        }
    }

    /// <summary>
    /// The number after the code, e.g. 2 in <c>IntrvlWkDay:02:03</c>, 0 when there isn't one.
    /// </summary>
    private static int Interval(string[] parts)
    {
        if (parts.Length < 2 || !int.TryParse(parts[1], out int interval))
        {
            return 0;
        }

        return interval;
    }
}
