namespace FinanceTracker.Ai.Tools.Bills;

/// <summary>
/// The user's next bill payments, soonest first.
/// </summary>
/// <param name="Payments">The payments.</param>
/// <param name="Link">The FinTrack page to check them on.</param>
public sealed record UpcomingPayments(List<UpcomingBill> Payments, string Link)
{
    /// <summary>
    /// The ISO 4217 code of the currency the amounts are in, e.g. GBP.
    /// </summary>
    public string? Currency { get; init; } = ToolCurrency.Code;
}
