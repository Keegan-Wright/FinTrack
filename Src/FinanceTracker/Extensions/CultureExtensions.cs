using System.Globalization;
using FinanceTracker.AppHost.ServiceDefaults;

namespace FinanceTracker.Extensions;

public static class CultureExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Uses the configured culture (e.g. <c>en-GB</c>) for dates and currency, the host's culture otherwise.
        /// </summary>
        public WebApplicationBuilder UseConfiguredCulture()
        {
            string? culture = builder.Configuration[FinTrackEnvironment.Culture];
            if (!string.IsNullOrEmpty(culture))
            {
                CultureInfo cultureInfo = new(culture);
                CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            }

            return builder;
        }
    }
}
