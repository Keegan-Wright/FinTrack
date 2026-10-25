using System.Globalization;

namespace FinanceTracker.Client.Components.Common;

/// <summary>
/// Money formatting shared across the app, see <see cref="MoneyText"/>.
/// </summary>
public static class CurrencyFormatter
{
    /// <summary>
    /// <paramref name="value"/> as money in the app's culture.
    /// </summary>
    public static string Format(decimal value) => value.ToString("C", CultureInfo.CurrentCulture);
}
