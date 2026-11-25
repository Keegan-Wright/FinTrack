namespace FinanceTracker.Ai.Tools.Spending;

/// <summary>
/// Money in and out over a period. Transfers between the user's own accounts aren't counted.
/// </summary>
/// <param name="From">The first day, null for no start.</param>
/// <param name="To">The last day, null up to today.</param>
/// <param name="MoneyIn">Money received, positive.</param>
/// <param name="MoneyOut">Money spent, positive.</param>
/// <param name="Net">Money in less money out.</param>
/// <param name="Transactions">How many transactions were counted.</param>
/// <param name="Link">The FinTrack page to check it on.</param>
public sealed record PeriodTotals(DateOnly? From, DateOnly? To, decimal MoneyIn, decimal MoneyOut, decimal Net,
    int Transactions, string Link)
{
    /// <summary>
    /// The ISO 4217 code of the currency the amounts are in, e.g. GBP.
    /// </summary>
    public string? Currency { get; init; } = ToolCurrency.Code;
}
