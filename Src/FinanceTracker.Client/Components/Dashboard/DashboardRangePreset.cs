namespace FinanceTracker.Client.Components.Dashboard;

/// <summary>
/// The date ranges the dashboard can show.
/// </summary>
public enum DashboardRangePreset
{
    /// <summary>
    /// From the 1st of this month to today.
    /// </summary>
    ThisMonth,

    /// <summary>
    /// The 30 days up to today.
    /// </summary>
    Last30Days,

    /// <summary>
    /// From the 1st of January to today.
    /// </summary>
    ThisYear
}
