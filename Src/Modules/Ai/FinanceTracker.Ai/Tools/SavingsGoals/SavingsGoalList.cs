namespace FinanceTracker.Ai.Tools.SavingsGoals;

/// <summary>
/// The user's savings goals.
/// </summary>
/// <param name="Goals">Each goal.</param>
/// <param name="Link">The FinTrack page to check them on.</param>
public sealed record SavingsGoalList(List<SavingsGoalSummary> Goals, string Link)
{
    /// <summary>
    /// The ISO 4217 code of the currency the amounts are in, e.g. GBP.
    /// </summary>
    public string? Currency { get; init; } = ToolCurrency.Code;
}
