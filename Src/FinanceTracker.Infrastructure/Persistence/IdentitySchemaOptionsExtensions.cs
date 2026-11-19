using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Gives a context built without the app's services the Identity schema the app uses. Identity reads its schema
/// version from the app's settings, which the design time tools and the tests' contexts don't have.
/// </summary>
public static class IdentitySchemaOptionsExtensions
{
    extension(DbContextOptionsBuilder<FinanceTrackerContext> builder)
    {
        /// <summary>
        /// Identity's tables at <see cref="FinanceTrackerContext.IdentitySchemaVersion"/>, passkeys included.
        /// </summary>
        public DbContextOptionsBuilder<FinanceTrackerContext> UseIdentitySchema()
        {
            ArgumentNullException.ThrowIfNull(builder);
            ServiceCollection services = new();
            services.Configure<IdentityOptions>(static options =>
                options.Stores.SchemaVersion = FinanceTrackerContext.IdentitySchemaVersion);
            return builder.UseApplicationServiceProvider(services.BuildServiceProvider());
        }
    }
}
