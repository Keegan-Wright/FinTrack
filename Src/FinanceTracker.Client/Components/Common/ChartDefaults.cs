using System.Globalization;
using ApexCharts;

namespace FinanceTracker.Client.Components.Common;

/// <summary>
/// The look every ApexChart in the app shares: transparent on its <see cref="Section"/>, the app's font and line
/// colours, no toolbar, and money formatted in the app's culture.
/// </summary>
public static class ChartDefaults
{
    public const string MoneyInColor = "#4CAF50";

    public const string MoneyOutColor = "#EF5350";

    public const string NeutralColor = "#42A5F5";

    /// <summary>
    /// The width in pixels below which a chart's responsive options apply, MudBlazor's small breakpoint.
    /// </summary>
    public const int PhoneBreakpoint = 600;

    /// <summary>
    /// Options for a money chart, re-create them when <paramref name="isDarkMode"/> changes.
    /// </summary>
    public static ApexChartOptions<T> Create<T>(bool isDarkMode) where T : class => new()
    {
        Chart = new Chart
        {
            Background = "transparent",
            FontFamily = "Inter, Roboto, Helvetica, Arial, sans-serif",
            ForeColor = isDarkMode ? "#B0B0B0" : "#757575",
            Toolbar = new Toolbar { Show = false },
            Zoom = new Zoom { Enabled = false }
        },
        Theme = new Theme { Mode = isDarkMode ? Mode.Dark : Mode.Light },
        Grid = new Grid { BorderColor = isDarkMode ? "#2A2A2A" : "#EEEEEE", StrokeDashArray = 4 },
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend { Position = LegendPosition.Top, HorizontalAlign = Align.Right },
        Tooltip = new Tooltip
        {
            Shared = true,
            Intersect = false,
            Y = new TooltipY { Formatter = MoneyFormatter(2) }
        },
        Yaxis = [new YAxis { Labels = new YAxisLabels { Formatter = MoneyFormatter(0) } }]
    };

    /// <summary>
    /// A JavaScript formatter writing a value as money in the app's culture, so it runs in the browser without a
    /// call back into .NET.
    /// </summary>
    public static string MoneyFormatter(int fractionDigits)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        string currency = GetIsoCurrency(culture);
        return $$"""
                 function (value) {
                     return new Intl.NumberFormat('{{culture.Name}}', {
                         style: 'currency', currency: '{{currency}}',
                         minimumFractionDigits: {{fractionDigits}}, maximumFractionDigits: {{fractionDigits}}
                     }).format(value);
                 }
                 """;
    }

    private static string GetIsoCurrency(CultureInfo culture)
    {
        try
        {
            return new RegionInfo(culture.Name).ISOCurrencySymbol;
        }
        catch (ArgumentException)
        {
            // The invariant and neutral cultures have no region
            return "GBP";
        }
    }
}
