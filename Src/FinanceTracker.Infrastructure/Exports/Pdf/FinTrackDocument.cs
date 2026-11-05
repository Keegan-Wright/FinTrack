using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// What every FinTrack PDF shares: A4 in the app's font and colours, a header with FinTrack, the document's title and
/// what it covers, and a footer with the day it was made and its page numbers. A document adds its content.
/// </summary>
public abstract class FinTrackDocument : IDocument
{
    protected FinTrackDocument(DateOnly madeOn)
    {
        QuestPdfSetup.EnsureConfigured();
        MadeOn = madeOn;
    }

    /// <summary>
    /// The day it was made, where the user is.
    /// </summary>
    protected DateOnly MadeOn { get; }

    /// <summary>
    /// e.g. Statement for November 2026, under FinTrack in the header, and the PDF's title.
    /// </summary>
    protected abstract string Title { get; }

    /// <summary>
    /// What it covers, at the header's right, e.g. 30 Oct to 27 Nov 2026.
    /// </summary>
    protected abstract string Covers { get; }

    /// <summary>
    /// A smaller line under <see cref="Covers"/>, e.g. Pay day to pay day, all accounts.
    /// </summary>
    protected abstract string CoversDetail { get; }

    /// <summary>
    /// Lays out what's between the header and footer, running onto as many pages as it needs.
    /// </summary>
    protected abstract void ComposeContent(IContainer container);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"FinTrack {Title}",
        Author = "FinTrack",
        Creator = "FinTrack",
        Language = PdfFormat.Culture.Name
    };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(BodySize).FontColor(TextPrimary));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingVertical(16).Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(title =>
                {
                    title.Item().Text("FinTrack").FontSize(16).Bold().FontColor(Primary);
                    title.Item().Text(Title).FontSize(12).SemiBold();
                });

                row.AutoItem().AlignBottom().Column(covers =>
                {
                    covers.Item().AlignRight().Text(Covers);
                    covers.Item().AlignRight().Text(CoversDetail).FontSize(SmallSize).FontColor(TextSecondary);
                });
            });

            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Divider);
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text($"Made by FinTrack on {PdfFormat.LongDate(MadeOn)}")
                .FontSize(SmallSize).FontColor(TextSecondary);

            row.AutoItem().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(SmallSize).FontColor(TextSecondary));
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }
}
