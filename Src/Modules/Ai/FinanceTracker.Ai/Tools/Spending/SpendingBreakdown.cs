namespace FinanceTracker.Ai.Tools.Spending;

/// <summary>
/// A period's money in and out split by tag or account, biggest spending first.
/// </summary>
/// <param name="From">The first day, null for no start.</param>
/// <param name="To">The last day, null up to today.</param>
/// <param name="By">What it's split by.</param>
/// <param name="Groups">The groups, at most <see cref="ToolText.MostRows"/>.</param>
/// <param name="MoreThanShown">Whether there were more groups than shown.</param>
/// <param name="Link">The FinTrack page to check it on.</param>
public sealed record SpendingBreakdown(DateOnly? From, DateOnly? To, SpendingGrouping By, List<SpendingGroup> Groups,
    bool MoreThanShown, string Link)
{
    /// <summary>
    /// The ISO 4217 code of the currency the amounts are in, e.g. GBP.
    /// </summary>
    public string? Currency { get; init; } = ToolCurrency.Code;
}
