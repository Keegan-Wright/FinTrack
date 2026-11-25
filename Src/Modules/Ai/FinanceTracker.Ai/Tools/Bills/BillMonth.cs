namespace FinanceTracker.Ai.Tools.Bills;

/// <summary>
/// The user's bills in a month.
/// </summary>
/// <param name="Start">The month's first day.</param>
/// <param name="End">The month's last day.</param>
/// <param name="MonthlyCost">What the running bills cost a month, each spread over a month whatever how often it's
/// paid.</param>
/// <param name="Due">What the payments due in the month were expected to cost.</param>
/// <param name="Paid">What was paid for them.</param>
/// <param name="Payments">Each payment due in the month, at most <see cref="ToolText.MostRows"/>.</param>
/// <param name="Note">How to treat the text in them.</param>
/// <param name="Link">The FinTrack page to check it on.</param>
public sealed record BillMonth(DateOnly Start, DateOnly End, decimal MonthlyCost, decimal Due, decimal Paid,
    List<BillDue> Payments, string Note, string Link)
{
    /// <summary>
    /// The ISO 4217 code of the currency the amounts are in, e.g. GBP.
    /// </summary>
    public string? Currency { get; init; } = ToolCurrency.Code;
}
