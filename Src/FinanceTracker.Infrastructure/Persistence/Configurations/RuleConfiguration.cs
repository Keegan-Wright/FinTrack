using FinanceTracker.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Rule"/> to its table.
/// </summary>
internal sealed class RuleConfiguration : AggregateRootConfiguration<Rule>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<Rule> builder)
    {
        builder.ToTable("Rules");

        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.MatchMode).HasConversion<string>();

        // Rules are loaded by user and run in order
        builder.HasIndex(x => new { x.UserId, x.Order });

        // Values hold what a user looks for in their transactions, e.g. a merchant's name, so they're encrypted.
        builder.ComplexJsonCollection(x => x.Conditions, conditions =>
        {
            conditions.Property(x => x.Field).HasConversion<string>();
            conditions.Property(x => x.Operator).HasConversion<string>();
            conditions.Property(x => x.Value).Encrypted();
            conditions.Property(x => x.SecondValue).Encrypted();
        });

        builder.ComplexJsonCollection(x => x.Actions, actions =>
        {
            actions.Property(x => x.Type).HasConversion<string>();
            actions.Property(x => x.BankClassification).Encrypted();
        });
    }
}
