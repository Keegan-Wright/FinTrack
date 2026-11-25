using FinanceTracker.Ai.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Ai;

/// <summary>
/// Plugs the AI module into the app. Only called by builds made with FinTrackAi=true.
/// </summary>
public static class AiModuleExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// The AI module's services and its tables in <paramref name="connectionString"/>'s database.
        /// </summary>
        /// <param name="connectionString">The app's database.</param>
        public WebApplicationBuilder AddFinTrackAi(string connectionString)
        {
            builder.Services.AddDbContextFactory<FinanceTrackerAiContext>(options =>
                FinanceTrackerAiContextOptions.Configure(options, connectionString));
            return builder;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// Applies the AI module's pending migrations, after the app's own.
        /// </summary>
        public async Task MigrateAiDatabaseAsync()
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            await using FinanceTrackerAiContext db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<FinanceTrackerAiContext>>()
                .CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }
    }
}
