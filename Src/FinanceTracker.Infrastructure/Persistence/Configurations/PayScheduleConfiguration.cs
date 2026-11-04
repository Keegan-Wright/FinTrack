using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.PayPeriods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class PayScheduleConfiguration : AggregateRootConfiguration<PaySchedule>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<PaySchedule> builder)
    {
        builder.ToTable("PaySchedules");

        // Who pays the user, how much and when, encrypted like their transactions
        builder.Property(x => x.Key).Encrypted();
        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.Amount).Encrypted();
        builder.Property(x => x.Rule).Encrypted();
        builder.Property(x => x.Day).Encrypted();
        builder.Property(x => x.PayDays).Encrypted();
        builder.PrimitiveCollection(x => x.PayDays).HasField("_payDays");
        builder.Ignore(x => x.Pattern);

        // One per user
        builder.HasIndex(x => x.UserId).IsUnique();

        // Goes with the account it's paid into, the next sync finds the pay again if it moved
        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
