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

        builder.HasIndex("AccountId", nameof(DirectDebit.ExternalId)).IsUnique();
    }
}
