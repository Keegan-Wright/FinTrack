using System.Globalization;

namespace FinanceTracker.Client.Common.Components;

/// <summary>
/// Tidies the text providers send for showing to people.
/// </summary>
public static class DisplayText
{
    /// <summary>
    /// Providers send types and categories shouting, e.g. <c>BILL_PAYMENT</c>, this makes them <c>Bill Payment</c>.
    /// </summary>
    /// <param name="value">The text.</param>
    public static string ToTitle(string value) =>
        CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Replace('_', ' ').ToLowerInvariant());
}
