using FinanceTracker.Ai.Backup;
using FinanceTracker.Ai.Contracts;
using FinanceTracker.Ai.Mcp;
using FinanceTracker.Ai.Persistence;
using FinanceTracker.Ai.Services;
using FinanceTracker.Ai.Tools;
using FinanceTracker.Infrastructure.Common.Encryption;
using FinanceTracker.Infrastructure.Common.Exports;
using FinanceTracker.Infrastructure.Common.Tokens;
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
            builder.Services.AddScoped<IAiService, AiService>();
            builder.Services.AddScoped<IBackupSection, AiBackupSection>();

            AiTools tools = new();
            builder.Services.AddSingleton<IAiTools>(tools);
            builder.Services.AddSingleton<PageLinks>();
            builder.Services.AddFinTrackMcp(tools);
            builder.Services.AddSingleton<ITokenScopeCheck, AiTokenScopeCheck>();
            return builder;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// Applies the AI module's pending migrations, after the app's own, then re-encrypts anything still under an
        /// older key, the same as the app does for its own tables.
        /// </summary>
        public async Task MigrateAiDatabaseAsync()
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            await using (FinanceTrackerAiContext db = await scope.ServiceProvider
                             .GetRequiredService<IDbContextFactory<FinanceTrackerAiContext>>()
                             .CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
            }

            await ActivatorUtilities
                .CreateInstance<EncryptionKeyRotator<FinanceTrackerAiContext>>(scope.ServiceProvider)
                .RotateAsync(CancellationToken.None);
        }
    }
}
