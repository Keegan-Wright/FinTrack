using FinanceTracker.AppHost.ServiceDefaults;
using Npgsql;

namespace FinanceTracker.Extensions;

public static class DatabaseConnectionExtensions
{
    extension(IConfiguration configuration)
    {
        /// <summary>
        /// The app's Postgres connection string, for everything that connects to it (EF Core and Quartz), with
        /// Kerberos (GSSAPI) encryption turned off. FinTrack signs in with a password, and the container has no
        /// Kerberos library, so trying it only logs that <c>libgssapi_krb5.so.2</c> can't be loaded on each start.
        /// </summary>
        /// <exception cref="InvalidOperationException">There's no connection string configured.</exception>
        public string GetFinTrackDatabaseConnectionString()
        {
            string configured = configuration.GetConnectionString(FinTrackResources.Database)
                                ?? throw new InvalidOperationException("The database connection string is missing");

            return new NpgsqlConnectionStringBuilder(configured)
            {
                GssEncryptionMode = GssEncryptionMode.Disable
            }.ConnectionString;
        }
    }
}
