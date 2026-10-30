using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class BillSuggestionConfiguration : AggregateRootConfiguration<BillSuggestion>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<BillSuggestion> builder)
    {
        builder.ToTable("BillSuggestions");

        // The payee is who the user pays, so it's encrypted like a bill's. Suggestions are few per user, so they're
        // compared once decrypted
        builder.Property(x => x.Key).Encrypted();
        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.Frequency).HasConversion<string>();
        builder.Property(x => x.Amount).Encrypted();
        builder.Property(x => x.FirstPaidOn).Encrypted();
        builder.Property(x => x.LastPaidOn).Encrypted();
        builder.PrimitiveCollection(x => x.PaymentIds).HasField("_paymentIds");

        // Goes with its account, like the account's bills
        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
