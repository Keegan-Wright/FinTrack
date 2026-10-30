using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// How often a bill is due.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BillFrequency>))]
public enum BillFrequency
{
    /// <summary>
    /// Every week.
    /// </summary>
    Weekly = 1,

    /// <summary>
    /// Every two weeks.
    /// </summary>
    Fortnightly = 2,

    /// <summary>
    /// Every four weeks, 13 times a year.
    /// </summary>
    FourWeekly = 3,

    /// <summary>
    /// Every month, on the same day.
    /// </summary>
    Monthly = 4,

    /// <summary>
    /// Every three months.
    /// </summary>
    Quarterly = 5,

    /// <summary>
    /// Every year.
    /// </summary>
    Yearly = 6
}
