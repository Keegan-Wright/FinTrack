using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// The PDFs' tables, close matching to the ApexCharts ones used. Small grey headings over rows split by faint lines.
/// </summary>
internal static class PdfTableExtensions
{
    extension(IContainer container)
    {
        /// <summary>
        /// A row's cell, to put its content in, kept whole so a row of two lines isn't split across pages.
        /// </summary>
        public IContainer TableCell() =>
            container.BorderBottom(1).BorderColor(TableLines).PaddingVertical(5).PaddingRight(6).ShowEntire();

        /// <summary>
        /// A column's heading, right aligned over amounts.
        /// </summary>
        public void TableHeaderCell(string text, bool alignRight = false)
        {
            IContainer cell = container.BorderBottom(1).BorderColor(Divider).PaddingBottom(4).PaddingRight(6);
            if (alignRight)
            {
                cell = cell.AlignRight();
            }

            cell.Text(text).FontSize(SmallSize).SemiBold().FontColor(TextSecondary);
        }

        /// <summary>
        /// What's shown in place of a table or list with nothing in it, e.g. No bills were due in this period.
        /// </summary>
        public void EmptyNote(string text) => container.Text(text).FontColor(TextSecondary);
    }
}
