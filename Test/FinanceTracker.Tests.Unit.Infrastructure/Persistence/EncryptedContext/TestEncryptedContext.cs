using FinanceTracker.Infrastructure.Common.Encryption;
using FinanceTracker.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence.EncryptedContext;

/// <summary>
/// A context other than the core's, set up from the shared project alone, the way a module's is.
/// </summary>
public sealed class TestEncryptedContext(DbContextOptions<TestEncryptedContext> options,
    IColumnEncryptionService encryptionService) : DbContext(options), IEncryptedContext
{
    public IColumnEncryptionService EncryptionService { get; } = encryptionService;

    public PendingRowIntegrityChecks PendingRowIntegrityChecks { get; } = new();

    public bool AcceptsOutdatedRowTags { get; set; }

    public TimeProvider TimeProvider { get; } = TimeProvider.System;

    public DbSet<TestNote> Notes => Set<TestNote>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseEncryptedContext();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestNote>().Property(static x => x.Text).Encrypted();
        modelBuilder.UseEncryption(EncryptionService);
        modelBuilder.UseRowIntegrity(static type => type == typeof(TestNote));
    }
}
