using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// Money in, money out and what's left as three <see cref="SummaryTile"/>s in a row, like the dashboard's, with any
/// notes under them.
/// </summary>
/// <param name="moneyIn">Money in, positive.</param>
/// <param name="moneyOut">Money out, positive.</param>
/// <param name="netLabel">What the third tile, in less out, is called, e.g. Net or Left over.</param>
/// <param name="notes">Each a line under the tiles, e.g. what isn't counted.</param>
internal sealed class InOutSummary(decimal moneyIn, decimal moneyOut, string netLabel, IReadOnlyList<string> notes)
    : IComponent
{
    public void Compose(IContainer container)
    {
        decimal net = moneyIn - moneyOut;

        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Component(new SummaryTile("Money in", moneyIn, MoneyIn));
                row.RelativeItem().Component(new SummaryTile("Money out", moneyOut, MoneyOut));
                row.RelativeItem().Component(new SummaryTile(netLabel, net, NetColour(net)));
            });

            foreach (string note in notes)
            {
                column.Item().PaddingTop(6).Text(note).FontSize(SmallSize).FontColor(TextSecondary);
            }
        });
    }
}
