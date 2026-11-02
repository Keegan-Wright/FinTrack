using System.ComponentModel;

namespace FinanceTracker.Contracts.Calendar;

/// <summary>
/// A debt's forecast pay-off day or its goal, on the calendar.
/// </summary>
public class CalendarDebtItem
{
    /// <summary>
    /// The debt's id.
    /// </summary>
    [Description("The debt's id")]
    public Guid DebtId { get; init; }

    /// <summary>
    /// The debt's name.
    /// </summary>
    [Description("Name of the debt")]
    public required string Name { get; init; }

    /// <summary>
    /// Which date this is.
    /// </summary>
    [Description("Whether this is the forecast pay-off day or the user's goal")]
    public CalendarDebtDate Kind { get; init; }

    /// <summary>
    /// For a goal, whether the forecast makes it. Null for the forecast itself.
    /// </summary>
    [Description("For a goal, whether the forecast makes it")]
    public bool? OnTrack { get; init; }
}
