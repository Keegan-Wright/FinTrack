using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class DebtConfiguration : AggregateRootConfiguration<Debt>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Debt> builder)
    {
        builder.ToTable("Debts");

        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.Lender).Encrypted();
        builder.Property(x => x.StartingBalance).Encrypted();
        builder.Property(x => x.StartedOn).Encrypted();
        builder.Property(x => x.InterestRate).Encrypted();
        builder.Property(x => x.MonthlyPayment).Encrypted();
        builder.Property(x => x.PayOffGoal).Encrypted();

        // One debt per name for each user in any case, paid off or not, see NameCheck
        string nameKey = builder.HasNameBlindIndex(x => x.Name);
        builder.HasIndex(nameof(Debt.UserId), nameKey).IsUnique();

        // Outlives its account, working out what's owed from its payments again
        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
