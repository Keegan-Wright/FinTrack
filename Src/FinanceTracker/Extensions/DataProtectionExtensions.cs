using FinanceTracker.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;

namespace FinanceTracker.Extensions;

/// <summary>
/// Sets up where ASP.NET Data Protection keeps its keys and how it encrypts.
/// </summary>
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
                .PersistKeysToDbContext<FinanceTrackerContext>()
                // AES-256-GCM like the encrypted columns, rather than the default AES-256-CBC with HMAC-SHA256.
                .UseCryptographicAlgorithms(new AuthenticatedEncryptorConfiguration
                {
                    EncryptionAlgorithm = EncryptionAlgorithm.AES_256_GCM,
                    ValidationAlgorithm = ValidationAlgorithm.HMACSHA256,
                });

            // Data Protection warns each new key is stored unencrypted, as it doesn't know EF encrypts the column
            builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager",
                LogLevel.Error);

            return builder;
        }
    }
}
