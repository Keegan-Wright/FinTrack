using System.Globalization;

namespace FinanceTracker.Components.Dashboard;

public static class CurrencyFormatter
{
    /// <summary>
    /// <paramref name="value"/> as money in the app's culture.
    /// </summary>
    public static string Format(decimal value) => value.ToString("C", CultureInfo.CurrentCulture);
}
