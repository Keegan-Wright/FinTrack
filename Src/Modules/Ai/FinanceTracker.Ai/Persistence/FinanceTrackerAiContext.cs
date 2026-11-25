using FinanceTracker.Infrastructure.Common.Encryption;
using FinanceTracker.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Ai.Persistence;

/// <summary>
/// The AI module's own tables. They live in their own schema with their own migrations history, so the plain build's
/// migrations never create them, and moving back to the plain image leaves them alone. Encrypted and tagged like the
/// core's, with the same keys.
/// </summary>
/// <param name="options">How it connects.</param>
/// <param name="encryptionService">Encrypts its columns and tags its rows, the app's own.</param>
public class FinanceTrackerAiContext(DbContextOptions<FinanceTrackerAiContext> options,
    IColumnEncryptionService encryptionService) : DbContext(options), IEncryptedContext
{
    /// <summary>
    /// The schema the AI module's tables live in.
    /// </summary>
    public const string Schema = "ai";

    /// <summary>
    /// The table the AI module's applied migrations are recorded in, inside <see cref="Schema"/>.
    /// </summary>
    public const string MigrationsHistoryTable = "__AiMigrationsHistory";

    /// <inheritdoc/>
    public IColumnEncryptionService EncryptionService { get; } = encryptionService;

    /// <inheritdoc/>
    public PendingRowIntegrityChecks PendingRowIntegrityChecks { get; } = new();

    /// <inheritdoc/>
    public bool AcceptsOutdatedRowTags { get; set; }

    /// <inheritdoc/>
    public TimeProvider TimeProvider { get; } = TimeProvider.System;

    /// <summary>
    /// The admin's app-wide settings.
    /// </summary>
    public DbSet<AiSetting> Settings => Set<AiSetting>();

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseEncryptedContext();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<AiSetting>(setting =>
        {
            setting.ToTable("Settings");
            setting.HasKey(static x => x.Name);
            setting.Property(static x => x.Value).Encrypted();
        });

        modelBuilder.UseEncryption(EncryptionService);
        // Every table here is the module's own, so every row carries a tag
        modelBuilder.UseRowIntegrity(static _ => true);
    }
}
