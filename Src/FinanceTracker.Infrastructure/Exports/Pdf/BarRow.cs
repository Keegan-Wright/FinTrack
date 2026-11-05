using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// A name, a <see cref="ProgressBar"/> and an amount on one line, e.g. a tag and what was spent on it, with the bar
/// against the biggest in the list.
/// </summary>
/// <param name="share">The bar's fill, from 0 to 1.</param>
internal sealed class BarRow(string label, decimal share, string amount, string colour) : IComponent
{
    public void Compose(IContainer container)
    {
        container.Row(row =>
        {
            row.Spacing(8);
            row.RelativeItem(3).Text(label);
            row.RelativeItem(5).AlignMiddle().Component(new ProgressBar(share, colour));
            row.ConstantItem(70).AlignRight().Text(amount);
        });
    }
}
