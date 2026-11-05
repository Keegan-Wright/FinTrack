using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfFormat;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// A table of what came in and went out for each of <paramref name="rows"/>: its name, how many transactions, in, out
/// and net, the net coloured by whether it's above or below zero.
/// </summary>
/// <param name="nameHeader">The first column's heading, e.g. Account or Month.</param>
internal sealed class TotalsTable(string nameHeader, IReadOnlyList<TotalsTableRow> rows) : IComponent
{
    private const float AmountWidth = 70;

    public void Compose(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.ConstantColumn(AmountWidth);
                columns.ConstantColumn(AmountWidth);
                columns.ConstantColumn(AmountWidth);
                columns.ConstantColumn(AmountWidth);
            });

            table.Header(header =>
            {
                header.Cell().TableHeaderCell(nameHeader);
                header.Cell().TableHeaderCell("Transactions", alignRight: true);
                header.Cell().TableHeaderCell("In", alignRight: true);
                header.Cell().TableHeaderCell("Out", alignRight: true);
                header.Cell().TableHeaderCell("Net", alignRight: true);
            });

            foreach (TotalsTableRow row in rows)
            {
                Row(table, row);
            }
        });
    }

    private static void Row(TableDescriptor table, TotalsTableRow row)
    {
        table.Cell().TableCell().Column(cell =>
        {
            cell.Item().Text(row.Name);
            if (row.Detail is not null)
            {
                cell.Item().Text(row.Detail).FontSize(SmallSize).FontColor(TextSecondary);
            }
        });
        table.Cell().TableCell().AlignRight().Text(row.Transactions.ToString("N0", Culture));
        table.Cell().TableCell().AlignRight().Text(Money(row.MoneyIn));
        table.Cell().TableCell().AlignRight().Text(Money(row.MoneyOut));
        table.Cell().TableCell().AlignRight().Text(Money(row.Net)).SemiBold().FontColor(NetColour(row.Net));
    }
}
