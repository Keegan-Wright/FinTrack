using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// Money in and out side by side for each period, like the reports page's chart, scaled to the biggest, with a
/// legend and the scale's top amount.
/// </summary>
internal sealed class InOutChart(IReadOnlyList<InOutChartBar> bars) : IComponent
{
    private const float PlotHeight = 90;

    public void Compose(IContainer container)
    {
        decimal biggest = bars.Select(static x => Math.Max(x.MoneyIn, x.MoneyOut)).DefaultIfEmpty(0).Max();

        container.Column(column =>
        {
            column.Item().Row(legend =>
            {
                legend.Spacing(12);
                legend.RelativeItem().Text($"Up to {PdfFormat.Money(biggest)}").FontSize(SmallSize)
                    .FontColor(TextSecondary);
                legend.AutoItem().Element(item => Key(item, MoneyIn, "Money in"));
                legend.AutoItem().Element(item => Key(item, MoneyOut, "Money out"));
            });

            column.Item().PaddingTop(6).BorderBottom(1).BorderColor(Divider).Height(PlotHeight).Row(plot =>
            {
                plot.Spacing(4);
                foreach (InOutChartBar bar in bars)
                {
                    plot.RelativeItem().AlignBottom().Row(pair =>
                    {
                        pair.Spacing(1);
                        pair.RelativeItem().AlignBottom().Element(x => Bar(x, bar.MoneyIn, biggest, MoneyIn));
                        pair.RelativeItem().AlignBottom().Element(x => Bar(x, bar.MoneyOut, biggest, MoneyOut));
                    });
                }
            });

            column.Item().PaddingTop(3).Row(labels =>
            {
                labels.Spacing(4);
                foreach (InOutChartBar bar in bars)
                {
                    labels.RelativeItem().AlignCenter().Text(bar.Label).FontSize(SmallSize)
                        .FontColor(TextSecondary);
                }
            });
        });
    }

    /// <summary>
    /// A bar as tall as <paramref name="amount"/> is of <paramref name="biggest"/>, nothing for nothing.
    /// </summary>
    private static void Bar(IContainer container, decimal amount, decimal biggest, string colour)
    {
        if (amount <= 0 || biggest <= 0)
        {
            return;
        }

        float height = Math.Max(1, (float)(amount / biggest) * PlotHeight);
        container.Height(height).Background(colour).CornerRadius(2);
    }

    private static void Key(IContainer container, string colour, string text)
    {
        container.Row(key =>
        {
            key.Spacing(4);
            key.AutoItem().AlignMiddle().Width(8).Height(8).Background(colour).CornerRadius(2);
            key.AutoItem().Text(text).FontSize(SmallSize).FontColor(TextSecondary);
        });
    }
}
