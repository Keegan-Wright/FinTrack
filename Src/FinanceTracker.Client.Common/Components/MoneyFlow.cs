namespace FinanceTracker.Client.Common.Components;

/// <summary>
/// Which way money goes, for the colour it's shown in.
/// </summary>
public enum MoneyFlow
{
    /// <summary>Neither in nor out, e.g. a balance.</summary>
    Neutral,
    /// <summary>
    /// Money in.
    /// </summary>
    In,
    /// <summary>
    /// Money out.
    /// </summary>
    Out
}
