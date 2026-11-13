using System.Globalization;
using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Transactions;
using MudBlazor;

namespace FinanceTracker.Client.Components.Bills;

/// <summary>
/// How bills read on screen. How often, where from, and how each period stands.
/// </summary>
public static class BillText
{
    public static string FrequencyName(BillFrequency frequency) => frequency switch
    {
        BillFrequency.Weekly => "Weekly",
        BillFrequency.Fortnightly => "Fortnightly",
        BillFrequency.FourWeekly => "Every four weeks",
        BillFrequency.Monthly => "Monthly",
        BillFrequency.Quarterly => "Quarterly",
        BillFrequency.Yearly => "Yearly",
        _ => frequency.ToString()
    };

    public static string SourceName(BillSource source) => source switch
    {
        BillSource.StandingOrder => "Standing order",
        BillSource.DirectDebit => "Direct debit",
        BillSource.Detected => "Subscription",
        BillSource.Manual => "Added by you",
        _ => source.ToString()
    };

    /// <summary>
    /// e.g. <c>Current · Monthly · Direct debit</c>.
    /// </summary>
    public static string Summary(Bill bill) =>
        $"{bill.AccountName} · {FrequencyName(bill.Frequency)} · {SourceName(bill.Source)}";

    /// <summary>
    /// Who said a transaction pays its bill, e.g. <c>Matched by FinTrack</c> or <c>By your rule Rent</c>, or
    /// <c>By Jane Doe's rule Rent</c> for another person's bill.
    /// </summary>
    public static string Reason(TransactionBill bill)
    {
        ArgumentNullException.ThrowIfNull(bill);
        return bill.DecidedBy switch
        {
            TransferDecidedBy.User => $"Chosen by {Who(bill.OwnerName)}",
            TransferDecidedBy.Rule => $"By {Whose(bill.OwnerName)} rule {bill.RuleName}",
            _ => "Matched by FinTrack"
        };
    }

    /// <summary>
    /// <c>you</c>, or the other person's name when it's theirs.
    /// </summary>
    internal static string Who(string? ownerName) => ownerName ?? "you";

    /// <summary>
    /// <c>your</c>, or <c>Jane Doe's</c> when it's theirs.
    /// </summary>
    internal static string Whose(string? ownerName)
    {
        if (ownerName is null)
        {
            return "your";
        }

        return $"{ownerName}'s";
    }

    /// <summary>
    /// How a bill reads where one is picked, telling apart bills with the same name, e.g.
    /// <c>Phone · Joint · £12.00 monthly</c>, with whose it is when someone else's, and <c>… · ended</c> once it has.
    /// </summary>
    public static string PickerLabel(Bill bill)
    {
        string label = $"{bill.Name} · {bill.AccountName} · {CurrencyFormatter.Format(bill.ExpectedAmount)} " +
                       FrequencyName(bill.Frequency).ToLowerInvariant();
        if (bill.SharedBy is { } sharedBy)
        {
            label = $"{label} · {sharedBy}'s";
        }

        if (bill.EndedOn is not null)
        {
            return $"{label} · ended";
        }

        return label;
    }

    /// <summary>
    /// e.g. <c>4 Nov</c>, with the year when it isn't this one.
    /// </summary>
    public static string Day(DateOnly day, DateOnly today)
    {
        string format = day.Year == today.Year ? "d MMM" : "d MMM yyyy";
        return day.ToString(format, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// When it's next due and when it was last paid, e.g. <c>Next due 12 Nov · Last paid £9.99 on 12 Oct</c>.
    /// </summary>
    public static string Dates(Bill bill, DateOnly today)
    {
        List<string> parts = [];

        if (bill.EndedOn is { } endedOn)
        {
            parts.Add($"Ended {Day(endedOn, today)}");
        }
        else if (bill.NextDue is { } nextDue)
        {
            parts.Add($"Next due {Day(nextDue, today)}");
        }

        if (bill.LastPaid is { } lastPaid && bill.LastPaidOn is { } lastPaidOn)
        {
            parts.Add($"Last paid {CurrencyFormatter.Format(lastPaid)} on {Day(lastPaidOn, today)}");
        }
        else
        {
            parts.Add("Not paid yet");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// One period on its chip, e.g. <c>Paid 12 Oct</c>, <c>Due 19 Oct</c> or <c>Late, due 3 Oct</c>.
    /// </summary>
    public static string PeriodLabel(BillPeriod period, DateOnly today)
    {
        string due = Day(period.Due, today);
        return period.State switch
        {
            BillState.Paid => $"Paid {Day(period.PaidOn ?? period.Due, today)}",
            BillState.Late => $"Late, due {due}",
            BillState.Changed => $"Paid {Day(period.PaidOn ?? period.Due, today)}, price changed",
            _ => $"Due {due}"
        };
    }

    /// <summary>
    /// What changed, e.g. <c>Paid £12.99, expected £9.99</c>, null unless the period's price changed.
    /// </summary>
    public static string? Changed(BillPeriod period)
    {
        if (period.State != BillState.Changed)
        {
            return null;
        }

        return $"Paid {CurrencyFormatter.Format(period.Paid)}, expected {CurrencyFormatter.Format(period.Expected)}";
    }

    public static string StateIcon(BillState state) => state switch
    {
        BillState.Paid => Icons.Material.Outlined.CheckCircle,
        BillState.Late => Icons.Material.Outlined.ErrorOutline,
        BillState.Changed => Icons.Material.Outlined.PriceChange,
        _ => Icons.Material.Outlined.Schedule
    };

    public static Color StateColor(BillState state) => state switch
    {
        BillState.Paid => Color.Success,
        BillState.Late => Color.Error,
        BillState.Changed => Color.Warning,
        _ => Color.Default
    };

    /// <summary>
    /// The month's total due and paid, e.g. <c>£120.00 of £340.00 paid</c>.
    /// </summary>
    public static string PaidOfDue(BillMonthOverview overview) =>
        $"{CurrencyFormatter.Format(overview.Paid)} of {CurrencyFormatter.Format(overview.Due)} paid";

    /// <summary>
    /// Whose a shared bill is and what the user can do, e.g. <c>Jane Doe's · you can change it</c>.
    /// </summary>
    public static string SharedText(string sharedBy, bool canChange)
    {
        if (canChange)
        {
            return $"{sharedBy}'s · you can change it";
        }

        return $"{sharedBy}'s · you can see it";
    }
}
