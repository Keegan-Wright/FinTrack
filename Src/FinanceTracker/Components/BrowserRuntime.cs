using System.Globalization;
using System.Text.Json;

namespace FinanceTracker.Components;

/// <summary>
/// The server's culture and local time zone, which <c>App.razor</c> hands to .NET in the browser before it starts.
/// The server's time is the app's time: a day, a month or "today" means the same in the browser as on the server,
/// whichever time zone the browser is in.
/// </summary>
internal static class BrowserRuntime
{
    /// <summary>
    /// The settings as a JSON object for the page's start script, <c>culture</c>
    /// and <c>timeZone</c> (an IANA id <c>TZ</c>).
    /// </summary>
    public static string SettingsJson() => JsonSerializer.Serialize(new
    {
        culture = CultureInfo.CurrentCulture.Name,
        timeZone = IanaTimeZoneId(TimeZoneInfo.Local)
    });

    private static string IanaTimeZoneId(TimeZoneInfo timeZone)
    {
        if (timeZone.HasIanaId)
        {
            return timeZone.Id;
        }

        // A Windows host names its zones differently, the browser's runtime only knows IANA ids
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out string? ianaId) ? ianaId : "UTC";
    }
}
