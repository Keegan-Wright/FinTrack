using System.Globalization;

namespace FinanceTracker.Infrastructure.Common.Encryption;

/// <summary>
/// A value written as text in the invariant culture and round-trip formats, so it doesn't depend on the host's locale.
/// What's encrypted, and what a row's integrity tag covers.
/// </summary>
internal static class PlainText
{
    /// <summary>
    /// The value as text, null for null.
    /// </summary>
    /// <param name="value">The value.</param>
    public static string? From<T>(T value) => value switch
    {
        null => null,
        string text => text,
        byte[] byteArray => Convert.ToBase64String(byteArray),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        DateOnly dateOnly => dateOnly.ToString("O", CultureInfo.InvariantCulture),
        System.Collections.IEnumerable enumerable => System.Text.Json.JsonSerializer.Serialize(enumerable),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
