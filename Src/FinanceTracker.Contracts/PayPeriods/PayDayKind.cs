using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.PayPeriods;

/// <summary>
/// Which day of the month pay comes in, see <see cref="Income"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PayDayKind>))]
public enum PayDayKind
{
    /// <summary>
    /// The month's last working day.
    /// </summary>
    LastWorkingDay = 1,

    /// <summary>
    /// The same day each month, the working day before when it falls on a weekend or bank holiday.
    /// </summary>
    DayOfMonth = 2
}
