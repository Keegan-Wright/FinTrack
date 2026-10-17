using FinanceTracker.Domain.Budgeting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class DebtConfiguration : AggregateRootConfiguration<Debt>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Debt> builder) =>
        builder.ToTable("Debts");
}
