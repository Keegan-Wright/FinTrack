using FinanceTracker.Domain.OpenBanking.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="SyncLogEntry"/> to its table.
/// </summary>
internal sealed class SyncLogEntryConfiguration : IEntityTypeConfiguration<SyncLogEntry>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SyncLogEntry> builder)
    {
        builder.ToTable("SyncLog");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>();

        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AccountId, x.OccurredAt });
        builder.HasIndex(x => x.OccurredAt);
    }
}
