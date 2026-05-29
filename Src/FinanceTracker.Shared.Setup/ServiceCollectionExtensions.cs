using System.Security.Claims;
using FinanceTracker.Configurations;
using FinanceTracker.Data;
using FinanceTracker.Security.Encryption;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinanceTracker.Shared.Setup;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddSharedDependencies(IConfiguration configuration)
        {
            AddEntityFramework(serviceCollection, configuration);
            AddHttpContextAndClaims(serviceCollection);
            AddEncryption(serviceCollection, configuration);

            return serviceCollection;
        }
    }

    private static void AddEntityFramework(IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.AddDbContextFactory<FinanceTrackerContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString(FinanceTrackerConstants.FinTrackDbName), npgsqlDbContextOptionsBuilder =>
            {
                npgsqlDbContextOptionsBuilder.MigrationsAssembly("FinanceTracker.Data.Migrations");
                npgsqlDbContextOptionsBuilder.EnableRetryOnFailure();
                npgsqlDbContextOptionsBuilder.CommandTimeout(0);
            });
        });
    }

    private static void AddHttpContextAndClaims(IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpContextAccessor();
        serviceCollection.AddScoped<ClaimsPrincipal?>(s =>
            s.GetService<IHttpContextAccessor>()?.HttpContext?.User ?? null);
    }

    private static void AddEncryption(IServiceCollection serviceCollection, IConfiguration configuration)
    {
        EncryptionConfiguration encryptionConfig = new()
        {
            SymmetricKey = configuration.GetValue<string>("ENCRYPTION_KEY")!,
            SymmetricSalt = configuration.GetValue<string>("ENCRYPTION_SALT")!,
            Iterations = configuration.GetValue<int>("ENCRYPTION_ITERATIONS")
        };

        serviceCollection.AddSingleton(encryptionConfig);

        serviceCollection.TryAddTransient<ISymmetricEncryptionService, SymmetricEncryptionService>();
    }


}
