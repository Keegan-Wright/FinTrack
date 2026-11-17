using FinanceTracker.Domain.OpenBanking.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="TransferMatchCheckpoint"/> to its table.
/// </summary>
internal sealed class TransferMatchCheckpointConfiguration : AggregateRootConfiguration<TransferMatchCheckpoint>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<TransferMatchCheckpoint> builder)
    {
        builder.ToTable("TransferMatchCheckpoints");

        // Compared with transactions' Created and Updated in the database, which aren't encrypted either
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
