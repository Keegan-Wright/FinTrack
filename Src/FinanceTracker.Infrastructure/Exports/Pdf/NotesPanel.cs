using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// A heading and a line for each of <paramref name="lines"/> on a tinted panel, e.g. the filters a report is narrowed
/// to under Showing only.
/// </summary>
internal sealed class NotesPanel(string heading, IReadOnlyList<string> lines) : IComponent
{
    public void Compose(IContainer container)
    {
        container.Background(Background).CornerRadius(CornerRadius).Padding(10).Column(panel =>
        {
            panel.Item().Text(heading).FontSize(SmallSize).SemiBold().FontColor(TextSecondary);
            foreach (string line in lines)
            {
                panel.Item().PaddingTop(2).Text(line).FontSize(SmallSize);
            }
        });
    }
}
