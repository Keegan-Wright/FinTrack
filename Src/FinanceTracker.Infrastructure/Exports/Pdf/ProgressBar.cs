using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// A thin rounded bar filled to <paramref name="share"/>, from 0 to 1, shown full past 1.
/// </summary>
internal sealed class ProgressBar(decimal share, string colour) : IComponent
{
    public void Compose(IContainer container)
    {
        float filled = (float)Math.Clamp(share, 0, 1);

        container.Height(6).Background(TableLines).CornerRadius(3).Row(row =>
        {
            if (filled > 0)
            {
                row.RelativeItem(filled).Background(colour).CornerRadius(3);
            }

            if (filled < 1)
            {
                row.RelativeItem(1 - filled);
            }
        });
    }
}
