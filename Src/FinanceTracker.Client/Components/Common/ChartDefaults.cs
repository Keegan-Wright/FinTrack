using System.Globalization;
using ApexCharts;
using FinanceTracker.Design;

namespace FinanceTracker.Client.Components.Common;

/// <summary>
/// The look every ApexChart in the app shares: transparent on its <see cref="Section"/>, the app's font and line
/// colours, no toolbar, and money formatted in the app's culture.
/// </summary>
public static class ChartDefaults
{
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
            FontFamily = AppTypeface.CssFontStack,
            ForeColor = isDarkMode ? DarkPalette.TextSecondary : LightPalette.TextSecondary,
            Toolbar = new Toolbar { Show = false },
            Zoom = new Zoom { Enabled = false }
        },
        Theme = new Theme { Mode = isDarkMode ? Mode.Dark : Mode.Light },
        Grid = new Grid { BorderColor = GridColour(isDarkMode), StrokeDashArray = 4 },
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
    /// The colour money going <paramref name="flow"/> is drawn in, in the mode the app is in, see
    /// <see cref="MoneyColours"/>.
    /// </summary>
    public static string MoneyColour(MoneyFlow flow, bool isDarkMode)
    {
        if (isDarkMode)
        {
            return flow switch
            {
                MoneyFlow.In => MoneyColours.DarkMoneyIn,
                MoneyFlow.Out => MoneyColours.DarkMoneyOut,
                _ => MoneyColours.DarkNeutral
            };
        }

        return flow switch
        {
            MoneyFlow.In => MoneyColours.LightMoneyIn,
            MoneyFlow.Out => MoneyColours.LightMoneyOut,
            _ => MoneyColours.LightNeutral
        };
    }

    /// <summary>
    /// In, out and neutral, the series order the cash flow and report charts use.
    /// </summary>
    public static List<string> InOutAndNeutral(bool isDarkMode) =>
    [
        MoneyColour(MoneyFlow.In, isDarkMode),
        MoneyColour(MoneyFlow.Out, isDarkMode),
        MoneyColour(MoneyFlow.Neutral, isDarkMode)
    ];

    /// <summary>
    /// The colour of a chart's grid lines, the theme's default line colour.
    /// </summary>
    public static string GridColour(bool isDarkMode) =>
        isDarkMode ? DarkPalette.LinesDefault : LightPalette.LinesDefault;

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
