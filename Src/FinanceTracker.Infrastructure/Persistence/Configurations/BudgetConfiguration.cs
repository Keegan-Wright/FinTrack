using FinanceTracker.Domain.Budgeting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class BudgetConfiguration : AggregateRootConfiguration<Budget>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");

        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.RolloverMode).HasConversion<string>();
        builder.Ignore(x => x.FirstMonth);

        // Ids, like a transaction's own tag ids, read with the budget and never queried on their own
        builder.PrimitiveCollection(x => x.TagIds).HasField("_tagIds");

        builder.OwnsMany(x => x.Limits, limits =>
        {
            limits.ToJson("Limits");
            limits.Property(x => x.From).Encrypted();
            limits.Property(x => x.Amount).Encrypted();
            limits.Ignore(x => x.Month);
        });
        builder.Navigation(x => x.Limits).HasField("_limits");
    }
}
