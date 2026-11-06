namespace FinanceTracker.Design;

/// <summary>
/// The colours money in and out are shown in everywhere: amounts on the pages, charts and PDFs. A shade for each mode,
/// as one dark enough for 4.5:1 (WCAG AA) on light surfaces is too dark on dark ones. The pages read them as CSS
/// variables, see the client's <c>MoneyColourStyles</c>, and the PDFs, on white paper, use the light shades.
/// </summary>
public static class MoneyColours
{
    /// <summary>
    /// Money coming in, in light mode, a green 5.3:1 on white and 4.6:1 on the 10% primary tint a selected row or
    /// today's date sits on.
    /// </summary>
    public const string LightMoneyIn = "#2B7A2F";

    /// <summary>Money going out, in light mode, a red 5.6:1 on white and 4.9:1 on the 10% primary tint.</summary>
    public const string LightMoneyOut = "#C62828";

    /// <summary>
    /// Neither in nor out in light mode, e.g. a balance or net worth line, a blue 5.7:1 on white and 5.0:1 on the 10%
    /// primary tint.
    /// </summary>
    public const string LightNeutral = "#1565C0";

    /// <summary>Money coming in, in dark mode, a green 6.5:1 on the dark surface.</summary>
    public const string DarkMoneyIn = "#4CAF50";

    /// <summary>Money going out, in dark mode, a red 5.2:1 on the dark surface.</summary>
    public const string DarkMoneyOut = "#EF5350";

    /// <summary>Neither in nor out in dark mode, a blue.</summary>
    public const string DarkNeutral = "#42A5F5";
}
