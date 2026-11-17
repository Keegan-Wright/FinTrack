using FinanceTracker.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="RuleCheckpoint"/> to its table.
/// </summary>
internal sealed class RuleCheckpointConfiguration : AggregateRootConfiguration<RuleCheckpoint>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<RuleCheckpoint> builder)
    {
        builder.ToTable("RuleCheckpoints");
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
