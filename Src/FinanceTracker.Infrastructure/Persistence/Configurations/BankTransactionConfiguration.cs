using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class BankTransactionConfiguration : AggregateRootConfiguration<BankTransaction>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("BankTransactions");

        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.ExternalId).Encrypted();
        builder.Property(x => x.Description).Encrypted();
        builder.Property(x => x.Type).Encrypted();
        builder.Property(x => x.Category).Encrypted();
        builder.Property(x => x.Amount).Encrypted();
        builder.Property(x => x.Currency).Encrypted();
        builder.Property(x => x.OccurredAt).Encrypted();
        builder.Property(x => x.ProviderClassifications).Encrypted();

        // Required so a transaction whose bank sent none of the fields still loads with empty metadata, not null
        builder.OwnsOne(x => x.Metadata, metadata =>
        {
            metadata.Property(x => x.ProviderReference).HasColumnName("Meta_ProviderReference").Encrypted();
            metadata.Property(x => x.ProviderCategory).HasColumnName("Meta_ProviderCategory").Encrypted();
            metadata.Property(x => x.MerchantName).HasColumnName("Meta_MerchantName").Encrypted();
            metadata.Property(x => x.CounterPartyPreferredName).HasColumnName("Meta_CounterPartyPreferredName")
                .Encrypted();
            metadata.Property(x => x.DebtorAccountName).HasColumnName("Meta_DebtorAccountName").Encrypted();
        });
        builder.Navigation(x => x.Metadata).IsRequired();

        string externalIdBlindIndex = builder.HasBlindIndex(x => x.ExternalId);
        builder.HasIndex(nameof(BankTransaction.AccountId), externalIdBlindIndex).IsUnique();

        builder.PrimitiveCollection(x => x.ProviderClassifications).HasField("_providerClassifications");
        builder.PrimitiveCollection(x => x.CustomClassificationIds).HasField("_customClassificationIds");
    }
}
