using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// A name and how it's going, e.g. Holiday £640.00 of £1,500.00, over a <see cref="ProgressBar"/>.
/// </summary>
/// <param name="name">What it is, e.g. a goal.</param>
/// <param name="detail">How it's going, e.g. £640.00 of £1,500.00.</param>
/// <param name="share">The bar's fill, from 0 to 1, null for no bar when there's nothing to fill towards.</param>
/// <param name="colour">The bar's colour.</param>
internal sealed class LabelledProgress(string name, string detail, decimal? share, string colour) : IComponent
{
    /// <inheritdoc/>
    public void Compose(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(name).SemiBold();
                row.AutoItem().Text(detail).FontColor(TextSecondary);
            });

            if (share is { } filled)
            {
                column.Item().PaddingTop(3).Component(new ProgressBar(filled, colour));
            }
        });
    }
}
