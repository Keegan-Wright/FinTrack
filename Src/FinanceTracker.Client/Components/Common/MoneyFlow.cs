namespace FinanceTracker.Client.Components.Common;

/// <summary>
/// Which way money goes, for the colour it's shown in, see <see cref="ChartDefaults.MoneyColour"/>.
/// </summary>
public enum MoneyFlow
{
    /// <summary>Neither in nor out, e.g. a balance.</summary>
    Neutral,
    In,
    Out
}
