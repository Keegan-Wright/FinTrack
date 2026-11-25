using System.Globalization;

namespace FinanceTracker.Client.Common.Components;

/// <summary>
/// Money formatting shared across the app, see <see cref="MoneyText"/>.
/// </summary>
public static class CurrencyFormatter
{
    /// <summary>
    /// <paramref name="value"/> as money in the app's culture.
    /// </summary>
    /// <param name="value">The amount.</param>
    public static string Format(decimal value) => value.ToString("C", CultureInfo.CurrentCulture);
}
