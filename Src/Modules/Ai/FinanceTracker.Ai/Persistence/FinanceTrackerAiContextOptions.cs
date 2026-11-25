using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Ai.Persistence;

/// <summary>
/// How <see cref="FinanceTrackerAiContext"/> connects, shared by the app and the design-time tools.
/// </summary>
internal static class FinanceTrackerAiContextOptions
{
    /// <summary>
    /// Uses Postgres at <paramref name="connectionString"/>, recording migrations in the AI module's own history table.
    /// </summary>
    /// <param name="options">The options to set.</param>
    /// <param name="connectionString">The database, the same one the app uses.</param>
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsHistoryTable(FinanceTrackerAiContext.MigrationsHistoryTable, FinanceTrackerAiContext.Schema);
            npgsql.EnableRetryOnFailure();
        });
}
