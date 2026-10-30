using FinanceTracker.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class RuleConfiguration : AggregateRootConfiguration<Rule>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Rule> builder)
    {
        builder.ToTable("Rules");

        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.MatchMode).HasConversion<string>();

        // Rules are loaded by user and run in order
        builder.HasIndex(x => new { x.UserId, x.Order });

        // Values hold what a user looks for in their transactions, e.g. a merchant's name, so they're encrypted.
        builder.OwnsMany(x => x.Conditions, conditions =>
        {
            conditions.ToJson("Conditions");
            conditions.Property(x => x.Field).HasConversion<string>();
            conditions.Property(x => x.Operator).HasConversion<string>();
            conditions.Property(x => x.Value).Encrypted();
            conditions.Property(x => x.SecondValue).Encrypted();
        });
        builder.Navigation(x => x.Conditions).HasField("_conditions");

        builder.OwnsMany(x => x.Actions, actions =>
        {
            actions.ToJson("Actions");
            actions.Property(x => x.Type).HasConversion<string>();
            actions.Property(x => x.BankClassification).Encrypted();
        });
        builder.Navigation(x => x.Actions).HasField("_actions");
    }
}
