using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// One headline figure on a rounded tile, e.g. Money in £2,450.00, like the dashboard's.
/// </summary>
/// <param name="colour">The figure's colour.</param>
internal sealed class SummaryTile(string label, decimal amount, string colour) : IComponent
{
    public void Compose(IContainer container)
    {
        container.Background(Background).CornerRadius(CornerRadius).Padding(12).Column(tile =>
        {
            tile.Item().Text(label).FontSize(SmallSize).FontColor(TextSecondary);
            tile.Item().PaddingTop(2).Text(PdfFormat.Money(amount)).FontSize(16).Bold().FontColor(colour);
        });
    }
}
