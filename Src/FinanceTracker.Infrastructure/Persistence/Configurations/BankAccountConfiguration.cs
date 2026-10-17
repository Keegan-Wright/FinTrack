using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class BankAccountConfiguration : AggregateRootConfiguration<BankAccount>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccounts");

        builder.HasOne<BankConnection>()
            .WithMany()
            .HasForeignKey(x => x.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ConnectionId, x.ExternalId }).IsUnique();

        builder.OwnsOne(x => x.Balance, balance =>
        {
            balance.Property(x => x.Available).HasColumnName("Balance_Available");
            balance.Property(x => x.Current).HasColumnName("Balance_Current");
            balance.Property(x => x.AsOf).HasColumnName("Balance_AsOf");
        });

        builder.HasMany(x => x.StandingOrders)
            .WithOne()
            .HasForeignKey("AccountId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.StandingOrders).HasField("_standingOrders");

        builder.HasMany(x => x.DirectDebits)
            .WithOne()
            .HasForeignKey("AccountId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.DirectDebits).HasField("_directDebits");
    }
}
