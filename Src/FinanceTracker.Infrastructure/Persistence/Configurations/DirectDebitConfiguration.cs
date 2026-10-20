using FinanceTracker.Domain.OpenBanking.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class DirectDebitConfiguration : IEntityTypeConfiguration<DirectDebit>
{
    public void Configure(EntityTypeBuilder<DirectDebit> builder)
    {
        builder.ToTable("DirectDebits");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property<Guid>("AccountId");

        builder.Property(x => x.ExternalId).Encrypted();
        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.Status).Encrypted();
        builder.Property(x => x.PreviousPaymentDate).Encrypted();
        builder.Property(x => x.PreviousPaymentAmount).Encrypted();
        builder.Property(x => x.Currency).Encrypted();
        builder.Property(x => x.Timestamp).Encrypted();

        builder.HasIndex("AccountId", nameof(DirectDebit.ExternalId)).IsUnique();
    }
}
