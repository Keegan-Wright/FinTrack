namespace FinanceTracker.Client.Components.Dashboard;

/// <summary>
/// One day or month of <see cref="CashflowChart"/>.
/// </summary>
/// <param name="Label">The day or month, as shown on the axis.</param>
/// <param name="In">Money in, positive.</param>
/// <param name="Out">Money out, positive.</param>
/// <param name="RunningNet">Money in less money out from the start of the range to the end of this point.</param>
public sealed record CashflowPoint(string Label, decimal In, decimal Out, decimal RunningNet);
