namespace FinanceTracker.Ai.Tools.NetWorth;

/// <summary>
/// The user's net worth now and how it's moved.
/// </summary>
/// <param name="Now">The latest net worth, null when there's no balance history yet.</param>
/// <param name="ChangeThisMonth">How much it's moved since the month started, null without the history.</param>
/// <param name="ChangeThisYear">How much it's moved since the year started, null without the history.</param>
/// <param name="Parts">What makes up today's net worth.</param>
/// <param name="Months">Month by month, oldest first, at most the last 24.</param>
/// <param name="Link">The FinTrack page to check it on.</param>
public sealed record NetWorthSummary(NetWorthMonth? Now, decimal? ChangeThisMonth, decimal? ChangeThisYear,
    List<NetWorthPart> Parts, List<NetWorthMonth> Months, string Link)
{
    /// <summary>
    /// The ISO 4217 code of the currency the amounts are in, e.g. GBP.
    /// </summary>
    public string? Currency { get; init; } = ToolCurrency.Code;
}
