using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Extensions;

public static class PersistenceExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// The Postgres database and the keyring its encrypted columns use.
        /// </summary>
        public WebApplicationBuilder AddFinTrackPersistence()
        {
            builder.Services.AddDbContextFactory<FinanceTrackerContext>((_, options) =>
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString(FinTrackResources.Database), npgsqlDbContextOptionsBuilder =>
                {
                    npgsqlDbContextOptionsBuilder.MigrationsAssembly("FinanceTracker.Infrastructure.Migrations");
                    npgsqlDbContextOptionsBuilder.EnableRetryOnFailure();
                    npgsqlDbContextOptionsBuilder.CommandTimeout(FinanceTrackerContext.CommandTimeoutSeconds);
                });
            });

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            EncryptionConfiguration encryptionConfig = EncryptionConfiguration.Parse(
                builder.Configuration.GetValue<string>(FinTrackEnvironment.EncryptionKeys)
                    ?? throw new InvalidOperationException($"{FinTrackEnvironment.EncryptionKeys} isn't configured."),
                builder.Configuration.GetValue<int>(FinTrackEnvironment.EncryptionCurrentKeyId));

            builder.Services.AddSingleton(encryptionConfig);

            return builder;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// Applies pending migrations, then re-encrypts anything still under an older key before the app serves
        /// requests.
        /// </summary>
        public async Task MigrateDatabaseAsync()
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

            await using (FinanceTrackerContext db = await scope.ServiceProvider
                             .GetRequiredService<IDbContextFactory<FinanceTrackerContext>>()
                             .CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
            }

            await ActivatorUtilities.CreateInstance<EncryptionKeyRotator>(scope.ServiceProvider)
                .RotateAsync(CancellationToken.None);
        }
    }
}
