using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="PersonalAccessToken"/> to its table.
/// </summary>
internal sealed class PersonalAccessTokenConfiguration : AggregateRootConfiguration<PersonalAccessToken>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<PersonalAccessToken> builder)
    {
        builder.ToTable("PersonalAccessTokens");

        // The user's own words, encrypted like their other text
        builder.Property(x => x.Name).Encrypted();

        // A hash of a random secret isn't itself secret, but encrypted, one put in the database without our key won't
        // read, so a token can't be made up there
        builder.Property(x => x.SecretHash).Encrypted();

        // Every request with a token finds it by its lookup id
        builder.Property(x => x.LookupId).HasMaxLength(32);
        builder.HasIndex(x => x.LookupId).IsUnique();

        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(x => x.UserId);
    }
}
