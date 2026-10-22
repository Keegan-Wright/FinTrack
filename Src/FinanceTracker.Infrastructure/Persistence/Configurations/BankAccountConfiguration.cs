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

        builder.Property(x => x.AccountType).Encrypted();
        builder.Property(x => x.DisplayName).Encrypted();
        builder.Property(x => x.Currency).Encrypted();

        builder.HasOne<BankConnection>()
            .WithMany()
            .HasForeignKey(x => x.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ConnectionId, x.ExternalId }).IsUnique();

        builder.OwnsOne(x => x.Balance, balance =>
        {
            balance.Property(x => x.Available).HasColumnName("Balance_Available").Encrypted();
            balance.Property(x => x.Current).HasColumnName("Balance_Current").Encrypted();
            balance.Property(x => x.AsOf).HasColumnName("Balance_AsOf").Encrypted();
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
