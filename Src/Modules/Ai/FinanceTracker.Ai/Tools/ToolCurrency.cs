using System.Globalization;

namespace FinanceTracker.Ai.Tools;

/// <summary>
/// The currency the tools' amounts are in, the one FinTrack shows them in, from the install's culture. Amounts are
/// never converted, so a model needs telling which currency they are or it guesses.
/// </summary>
internal static class ToolCurrency
{
    /// <summary>
    /// The currency's ISO 4217 code, e.g. GBP, null when the culture has no country to give one.
    /// </summary>
    public static string? Code => Region()?.ISOCurrencySymbol;

    /// <summary>
    /// The currency's symbol as FinTrack shows it, e.g. £.
    /// </summary>
    public static string Symbol => CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol;

    /// <summary>
    /// The country of the install's culture, null for a culture without one, e.g. the invariant culture.
    /// </summary>
    private static RegionInfo? Region()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        if (culture.IsNeutralCulture || string.IsNullOrEmpty(culture.Name))
        {
            return null;
        }

        return new RegionInfo(culture.Name);
    }
}
