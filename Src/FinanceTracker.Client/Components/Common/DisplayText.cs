using System.Globalization;

namespace FinanceTracker.Client.Components.Common;

/// <summary>
/// Tidies the text providers send for showing to people.
/// </summary>
public static class DisplayText
{
    /// <summary>
    /// Providers send types and categories shouting, e.g. <c>BILL_PAYMENT</c>, this makes them <c>Bill Payment</c>.
    /// </summary>
    public static string ToTitle(string value) =>
        CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Replace('_', ' ').ToLowerInvariant());
}
