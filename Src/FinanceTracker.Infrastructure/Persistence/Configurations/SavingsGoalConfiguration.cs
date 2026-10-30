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

        // Where the saved amount comes from. The account and tags are ids, like a budget's, the day is encrypted with
        // the amounts
        builder.Property(x => x.Source).HasConversion<string>().HasDefaultValue(SavingsGoalSource.Manual)
            .HasSentinel((SavingsGoalSource)0);
        builder.Property(x => x.TrackedFrom).Encrypted();
        builder.PrimitiveCollection(x => x.TagIds).HasField("_tagIds");
        builder.Ignore(x => x.CoversNothing);

        // One goal per account, so its balance isn't counted twice
        builder.HasIndex(x => x.AccountId).IsUnique().HasFilter("\"AccountId\" IS NOT NULL");
    }
}
