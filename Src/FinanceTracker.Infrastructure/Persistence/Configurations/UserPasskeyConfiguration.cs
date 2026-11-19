using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Encrypts the passkeys' names and public keys. A public key isn't secret, but encrypting it means a key put in the
/// database without our encryption key won't read, so it can't be used to sign in.
/// </summary>
internal sealed class UserPasskeyConfiguration : IEntityTypeConfiguration<IdentityUserPasskey<Guid>>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdentityUserPasskey<Guid>> builder) =>
        builder.OwnsOne(x => x.Data, data =>
        {
            data.Property(x => x.Name).Encrypted();
            data.Property(x => x.PublicKey).Encrypted();
        });
}
