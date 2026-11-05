using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// A titled part of a document, a heading, a caption under it, then its content.
/// </summary>
/// <param name="caption">Under the title, none when null.</param>
internal sealed class PdfSection(string title, string? caption, Action<IContainer> content) : IComponent
{
    public void Compose(IContainer container)
    {
        container.Column(section =>
        {
            section.Item().Text(title).FontSize(12).SemiBold();
            if (caption is not null)
            {
                section.Item().PaddingTop(2).Text(caption).FontSize(SmallSize).FontColor(TextSecondary);
            }

            section.Item().PaddingTop(8).Element(content);
        });
    }
}
