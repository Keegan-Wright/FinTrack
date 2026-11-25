namespace FinanceTracker.Ai.Tools.Debts;

/// <summary>
/// The user's debts.
/// </summary>
/// <param name="Debts">Each debt.</param>
/// <param name="TotalOwed">What's owed across them all.</param>
/// <param name="Link">The FinTrack page to check them on.</param>
public sealed record DebtList(List<DebtSummary> Debts, decimal TotalOwed, string Link)
{
    /// <summary>
    /// The ISO 4217 code of the currency the amounts are in, e.g. GBP.
    /// </summary>
    public string? Currency { get; init; } = ToolCurrency.Code;
}
