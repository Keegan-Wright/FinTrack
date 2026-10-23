using FinanceTracker.Infrastructure;
using Microsoft.AspNetCore.DataProtection;

namespace FinanceTracker.Extensions;

public static class DataProtectionExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Keeps ASP.NET Data Protection's keys in the database, so a redeploy doesn't sign everyone out or void the
        /// forms they have open. Their XML is an encrypted column, see <c>DataProtectionKeyConfiguration</c>.
        /// </summary>
        public WebApplicationBuilder AddFinTrackDataProtection()
        {
            builder.Services.AddDataProtection()
                // Fixed, the default is the content root, which differs between machines and container images
                .SetApplicationName("FinTrack")
                .PersistKeysToDbContext<FinanceTrackerContext>();

            // Data Protection warns each new key is stored unencrypted, as it doesn't know EF encrypts the column
            builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager",
                LogLevel.Error);

            return builder;
        }
    }
}
