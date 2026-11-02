using FinanceTracker.Domain.Classifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class CustomClassificationConfiguration : AggregateRootConfiguration<CustomClassification>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<CustomClassification> builder)
    {
        builder.ToTable("CustomClassifications");

        builder.Property(x => x.Tag).Encrypted();

        // One tag per name for each user in any case, see NameCheck
        string nameKey = builder.HasNameBlindIndex(x => x.Tag);
        builder.HasIndex(nameof(CustomClassification.UserId), nameKey).IsUnique();
    }
}
