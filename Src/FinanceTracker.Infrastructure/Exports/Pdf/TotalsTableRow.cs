namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// One line of a <see cref="TotalsTable"/>.
/// </summary>
/// <param name="Name">What it totals, e.g. an account or a month.</param>
/// <param name="Detail">Under the name, e.g. a pay period's dates, none when null.</param>
/// <param name="MoneyIn">Positive.</param>
/// <param name="MoneyOut">Positive.</param>
internal sealed record TotalsTableRow(
    string Name,
    string? Detail,
    int Transactions,
    decimal MoneyIn,
    decimal MoneyOut)
{
    public decimal Net => MoneyIn - MoneyOut;
}
