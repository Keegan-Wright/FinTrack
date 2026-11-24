using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="BalanceSnapshot"/> to its table.
/// </summary>
internal sealed class BalanceSnapshotConfiguration : AggregateRootConfiguration<BalanceSnapshot>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<BalanceSnapshot> builder)
    {
        builder.ToTable("BalanceSnapshots");

        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Not encrypted, so history can be read by date range. It only shows which days had a sync, as Created does
        builder.HasIndex(x => new { x.AccountId, x.Date }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Date });

        builder.ComplexProperty(x => x.Balance, balance =>
        {
            balance.Property(x => x.Available).HasColumnName("Balance_Available").Encrypted();
            balance.Property(x => x.Current).HasColumnName("Balance_Current").Encrypted();
            balance.Property(x => x.AsOf).HasColumnName("Balance_AsOf").Encrypted();
            balance.Property(x => x.CreditLimit).HasColumnName("Balance_CreditLimit").Encrypted();
        });

        builder.Property(x => x.Source).HasConversion<string>();
    }
}
