namespace FinanceTracker.Design;

/// <summary>
/// The colours money in and out are shown in everywhere. Amounts on the pages, charts and PDFs. The same in light
/// and dark mode. The pages read them as CSS variables, see the client's <c>MoneyColourStyles</c>.
/// </summary>
public static class MoneyColours
{
    /// <summary>Money coming in, a green.</summary>
    public const string MoneyIn = "#4CAF50";

    /// <summary>Money going out, a red.</summary>
    public const string MoneyOut = "#EF5350";

    /// <summary>Neither in nor out, e.g. a balance or net worth line, a blue.</summary>
    public const string Neutral = "#42A5F5";
}
