using System.Globalization;

namespace FinanceTracker.Client.Components.Dashboard;

/// <summary>
/// Money formatting shared by the dashboard cards.
/// </summary>
public static class CurrencyFormatter
{
    /// <summary>
    /// <paramref name="value"/> as money in the app's culture.
    /// </summary>
    public static string Format(decimal value) => value.ToString("C", CultureInfo.CurrentCulture);
}
