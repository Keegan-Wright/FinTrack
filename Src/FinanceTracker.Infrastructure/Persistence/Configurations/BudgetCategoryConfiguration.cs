using FinanceTracker.Domain.Budgeting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class BudgetCategoryConfiguration : AggregateRootConfiguration<BudgetCategory>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<BudgetCategory> builder)
    {
        builder.ToTable("BudgetCategories");

        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.AvailableFunds).Encrypted();
        builder.Property(x => x.MonthlyStart).Encrypted();
        builder.Property(x => x.SavingsGoal).Encrypted();
        builder.Property(x => x.GoalCompletionDate).Encrypted();
    }
}
