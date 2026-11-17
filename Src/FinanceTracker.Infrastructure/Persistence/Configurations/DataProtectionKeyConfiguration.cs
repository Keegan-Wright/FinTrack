using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="DataProtectionKey"/> to its table.
/// </summary>
internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        // The key material, anyone holding it could forge a sign in cookie
        builder.Property(x => x.Xml).Encrypted();
    }
}
