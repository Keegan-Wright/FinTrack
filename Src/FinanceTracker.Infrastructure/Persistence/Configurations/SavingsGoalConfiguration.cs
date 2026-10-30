using FinanceTracker.Domain.SavingsGoals;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class SavingsGoalConfiguration : AggregateRootConfiguration<SavingsGoal>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<SavingsGoal> builder)
    {
        builder.ToTable("SavingsGoals");

        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.Saved).Encrypted();
        builder.Property(x => x.MonthlyContribution).Encrypted();
        builder.Property(x => x.Target).Encrypted();
        builder.Property(x => x.TargetDate).Encrypted();
    }
}
