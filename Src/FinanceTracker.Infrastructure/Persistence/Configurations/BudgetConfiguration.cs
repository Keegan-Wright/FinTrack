using FinanceTracker.Domain.Budgeting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Budget"/> to its table.
/// </summary>
internal sealed class BudgetConfiguration : AggregateRootConfiguration<Budget>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");

        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.RolloverMode).HasConversion<string>();
        builder.Ignore(x => x.FirstMonth);

        // Ids, like a transaction's own tag ids, read with the budget and never queried on their own
        builder.PrimitiveCollection(x => x.TagIds).HasField("_tagIds");
        builder.PrimitiveCollection(x => x.AccountIds).HasField("_accountIds");

        builder.ComplexJsonCollection(x => x.Limits, limits =>
        {
            limits.Property(x => x.From).Encrypted();
            limits.Property(x => x.Amount).Encrypted();
        });
    }
}
