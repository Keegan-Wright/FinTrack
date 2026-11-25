using FinanceTracker.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Encrypts the passkeys' names and public keys, and the record of how each was made. A public key isn't secret, but
/// encrypting it means a key put in the database without our encryption key won't read, so it can't be used to sign
/// in. The record holds another copy of the public key, the site's address and which password manager or device made
/// the passkey. Only dates, counters and flags are left readable.
/// </summary>
internal sealed class UserPasskeyConfiguration : IEntityTypeConfiguration<IdentityUserPasskey<Guid>>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdentityUserPasskey<Guid>> builder) =>
        builder.ComplexProperty(x => x.Data, data =>
        {
            data.Property(x => x.Name).Encrypted();
            data.Property(x => x.PublicKey).Encrypted();
            data.Property(x => x.AttestationObject).Encrypted();
            data.Property(x => x.ClientDataJson).Encrypted();
            data.Property(x => x.Aaguid).Encrypted();
        });
}
