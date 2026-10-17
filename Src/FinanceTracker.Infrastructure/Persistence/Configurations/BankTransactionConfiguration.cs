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

        builder.HasIndex(x => new { x.AccountId, x.ExternalId }).IsUnique();

        builder.PrimitiveCollection(x => x.ProviderClassifications).HasField("_providerClassifications");
        builder.PrimitiveCollection(x => x.CustomClassificationIds).HasField("_customClassificationIds");
    }
}
