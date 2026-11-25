using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Ai.Persistence;

/// <summary>
/// The AI module's own tables. They live in their own schema with their own migrations history, so the plain build's
/// migrations never create them, and moving back to the plain image leaves them alone.
/// </summary>
/// <param name="options">How it connects.</param>
public class FinanceTrackerAiContext(DbContextOptions<FinanceTrackerAiContext> options) : DbContext(options)
{
    /// <summary>
    /// The schema the AI module's tables live in.
    /// </summary>
    public const string Schema = "ai";

    /// <summary>
    /// The table the AI module's applied migrations are recorded in, inside <see cref="Schema"/>.
    /// </summary>
    public const string MigrationsHistoryTable = "__AiMigrationsHistory";

    /// <summary>
    /// The admin's app-wide settings.
    /// </summary>
    public DbSet<AiSetting> Settings => Set<AiSetting>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<AiSetting>(setting =>
        {
            setting.ToTable("Settings");
            setting.HasKey(static x => x.Name);
        });
    }
}
