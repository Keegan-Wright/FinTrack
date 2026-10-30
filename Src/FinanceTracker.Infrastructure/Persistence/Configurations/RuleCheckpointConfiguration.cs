using FinanceTracker.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class RuleCheckpointConfiguration : AggregateRootConfiguration<RuleCheckpoint>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<RuleCheckpoint> builder)
    {
        builder.ToTable("RuleCheckpoints");
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
