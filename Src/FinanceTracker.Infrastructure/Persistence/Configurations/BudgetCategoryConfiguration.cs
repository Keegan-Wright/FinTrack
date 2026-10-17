using FinanceTracker.Domain.Budgeting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class BudgetCategoryConfiguration : AggregateRootConfiguration<BudgetCategory>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<BudgetCategory> builder) =>
        builder.ToTable("BudgetCategories");
}
