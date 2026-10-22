using FinanceTracker.Domain.OpenBanking.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class StandingOrderConfiguration : IEntityTypeConfiguration<StandingOrder>
{
    public void Configure(EntityTypeBuilder<StandingOrder> builder)
    {
        builder.ToTable("StandingOrders");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property<Guid>("AccountId");

        builder.Property(x => x.Reference).Encrypted();
        builder.Property(x => x.Payee).Encrypted();
        builder.Property(x => x.Frequency).Encrypted();
        builder.Property(x => x.Status).Encrypted();
        builder.Property(x => x.Currency).Encrypted();
        builder.Property(x => x.NextPaymentDate).Encrypted();
        builder.Property(x => x.NextPaymentAmount).Encrypted();
        builder.Property(x => x.FirstPaymentDate).Encrypted();
        builder.Property(x => x.FirstPaymentAmount).Encrypted();
        builder.Property(x => x.FinalPaymentDate).Encrypted();
        builder.Property(x => x.FinalPaymentAmount).Encrypted();
        builder.Property(x => x.Timestamp).Encrypted();
    }
}
