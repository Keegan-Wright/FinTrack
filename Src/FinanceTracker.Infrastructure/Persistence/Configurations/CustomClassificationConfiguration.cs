using FinanceTracker.Domain.Classifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class CustomClassificationConfiguration : AggregateRootConfiguration<CustomClassification>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<CustomClassification> builder)
    {
        builder.ToTable("CustomClassifications");

        builder.HasIndex(x => new { x.UserId, x.Tag }).IsUnique();
    }
}
