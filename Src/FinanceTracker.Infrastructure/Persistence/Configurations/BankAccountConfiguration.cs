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

        // Whether it's an account or a card, which says no more than the account type next to it
        builder.Property(x => x.Kind).HasConversion<string>();

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
            balance.Property(x => x.CreditLimit).HasColumnName("Balance_CreditLimit").Encrypted();
        });

        builder.OwnsOne(x => x.Card, card =>
        {
            card.Property(x => x.Network).HasColumnName("Card_Network").Encrypted();
            card.Property(x => x.CardType).HasColumnName("Card_CardType").Encrypted();
            card.Property(x => x.PartialNumber).HasColumnName("Card_PartialNumber").Encrypted();
            card.Property(x => x.NameOnCard).HasColumnName("Card_NameOnCard").Encrypted();
            card.Property(x => x.ValidTo).HasColumnName("Card_ValidTo").Encrypted();
        });

        builder.OwnsOne(x => x.Identifier, identifier =>
        {
            identifier.Property(x => x.SortCode).HasColumnName("Identifier_SortCode").Encrypted();
            identifier.Property(x => x.Number).HasColumnName("Identifier_Number").Encrypted();
            identifier.Property(x => x.Iban).HasColumnName("Identifier_Iban").Encrypted();
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
