namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// One period on an <see cref="InOutChart"/>.
/// </summary>
/// <param name="Label">Under the bars, e.g. Nov.</param>
/// <param name="MoneyIn">0 or more.</param>
/// <param name="MoneyOut">As a positive amount, 0 or more.</param>
internal sealed record InOutChartBar(string Label, decimal MoneyIn, decimal MoneyOut);
