using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class BillConfiguration : AggregateRootConfiguration<Bill>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("Bills");

        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.Payee).Encrypted();
        builder.Property(x => x.Source).HasConversion<string>();
        builder.Property(x => x.Frequency).HasConversion<string>();
        builder.Property(x => x.StartsOn).Encrypted();
        builder.Property(x => x.Anchor).Encrypted();
        builder.Property(x => x.EndedOn).Encrypted();
        builder.Ignore(x => x.HasEnded);
        builder.Ignore(x => x.ExpectedAmount);

        builder.OwnsMany(x => x.Amounts, amounts =>
        {
            amounts.ToJson("Amounts");
            amounts.Property(x => x.From).Encrypted();
            amounts.Property(x => x.Amount).Encrypted();
        });
        builder.Navigation(x => x.Amounts).HasField("_amounts");

        // Goes with its account, like the account's transactions
        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // One bill per standing order or direct debit, so a sync seeds each once
        builder.HasIndex(x => x.SourceId).IsUnique().HasFilter("\"SourceId\" IS NOT NULL");
    }
}
