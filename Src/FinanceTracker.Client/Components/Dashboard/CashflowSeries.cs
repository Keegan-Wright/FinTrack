using System.Globalization;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Dashboard;

/// <summary>
/// Buckets transactions into <see cref="CashflowChart"/>'s points: a point a day, or a month for ranges longer than
/// <see cref="MaxDailyBars"/> days, with the running net across the range.
/// </summary>
public static class CashflowSeries
{
    /// <summary>
    /// Ranges longer than this many days are charted per month, daily bars get too thin to read.
    /// </summary>
    public const int MaxDailyBars = 62;

    /// <summary>
    /// Whether the range from <paramref name="from"/> to <paramref name="to"/> (local days, both included) is charted
    /// per month.
    /// </summary>
    public static bool IsByMonth(DateTime from, DateTime to) => Days(from, to) > MaxDailyBars;

    /// <summary>
    /// A point for every day or month of the range, empty ones included, from the transactions inside it (by local
    /// day). Empty when there are no transactions.
    /// </summary>
    public static List<CashflowPoint> Build(IReadOnlyCollection<Transaction> transactions, DateTime from, DateTime to)
    {
        DateTime start = from.Date;
        DateTime end = to.Date;
        int days = Days(from, to);
        if (transactions.Count == 0 || days <= 0)
        {
            return [];
        }

        bool byMonth = days > MaxDailyBars;
        int buckets = byMonth ? (end.Year - start.Year) * 12 + end.Month - start.Month + 1 : days;
        decimal[] inByBucket = new decimal[buckets];
        decimal[] outByBucket = new decimal[buckets];

        foreach (Transaction transaction in transactions)
        {
            DateTime day = transaction.TransactionTime.ToLocalTime().Date;
            if (day < start || day > end)
            {
                continue;
            }

            int index = byMonth ? (day.Year - start.Year) * 12 + day.Month - start.Month : (day - start).Days;
            if (transaction.Amount >= 0)
            {
                inByBucket[index] += transaction.Amount;
            }
            else
            {
                outByBucket[index] += Math.Abs(transaction.Amount);
            }
        }

        List<CashflowPoint> points = new(buckets);
        decimal runningNet = 0m;
        for (int i = 0; i < buckets; i++)
        {
            runningNet += inByBucket[i] - outByBucket[i];
            string label = byMonth
                ? start.AddMonths(i).ToString("MMM", CultureInfo.CurrentCulture)
                : start.AddDays(i).ToString("d MMM", CultureInfo.CurrentCulture);
            points.Add(new CashflowPoint(label, inByBucket[i], outByBucket[i], runningNet));
        }

        return points;
    }

    private static int Days(DateTime from, DateTime to) => (to.Date - from.Date).Days + 1;
}
