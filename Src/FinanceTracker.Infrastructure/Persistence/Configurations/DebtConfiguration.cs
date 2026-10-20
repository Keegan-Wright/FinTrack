using FinanceTracker.Domain.Budgeting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class DebtConfiguration : AggregateRootConfiguration<Debt>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Debt> builder)
    {
        builder.ToTable("Debts");

        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.MonthlyPayment).Encrypted();
        builder.Property(x => x.FinalPaymentDate).Encrypted();
        builder.Property(x => x.PayOffGoal).Encrypted();
    }
}
