using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Identity;

/// <summary>
/// A token a script or Home Assistant reads the user's data with, instead of signing in. Only a hash of its secret is
/// kept, so it can't be shown again once made. It stops working when it expires or is revoked.
/// </summary>
public sealed class PersonalAccessToken : AggregateRoot
{
    /// <summary>
    /// How often using a token is recorded at most, so a frequent poll doesn't write on every request.
    /// </summary>
    public static readonly TimeSpan UseRecordedEvery = TimeSpan.FromMinutes(1);

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private PersonalAccessToken()
    {
    }
#pragma warning restore CS8618

    /// <param name="userId">The user it reads for.</param>
    /// <param name="name">What it's for, e.g. <c>Home Assistant</c>.</param>
    /// <param name="lookupId">The public part of the token it's found by.</param>
    /// <param name="secretHash">The SHA-256 hash of the secret part.</param>
    /// <param name="expiresAt">When it stops working, in UTC, null for never.</param>
    /// <exception cref="ArgumentException">The name or lookup id is blank, or there's no hash.</exception>
    public PersonalAccessToken(Guid userId, string name, string lookupId, byte[] secretHash, DateTime? expiresAt)
        : base(userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(lookupId);
        ArgumentNullException.ThrowIfNull(secretHash);
        ArgumentOutOfRangeException.ThrowIfZero(secretHash.Length);
        Name = name.Trim();
        LookupId = lookupId;
        SecretHash = secretHash;
        Scope = TokenScope.Read;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// What it's for.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The public part of the token, unique, that it's found by.
    /// </summary>
    public string LookupId { get; private set; }

    /// <summary>
    /// The SHA-256 hash of the token's secret part. The secret itself is never kept.
    /// </summary>
        public byte[] SecretHash { get; private set; }

    /// <summary>
    /// What it may do.
    /// </summary>
    public TokenScope Scope { get; private set; }

    /// <summary>
    /// When it stops working, in UTC, null for never.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// When it was last used, in UTC, to the nearest <see cref="UseRecordedEvery"/>. Null before its first use.
    /// </summary>
    public DateTime? LastUsedAt { get; private set; }

    /// <summary>
    /// When it was revoked, in UTC, null while it hasn't been.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// Whether it still works at <paramref name="now"/>, not revoked or expired.
    /// </summary>
    /// <param name="now">The time now, in UTC.</param>
    public bool IsActive(DateTime now)
    {
        if (RevokedAt is not null)
        {
            return false;
        }

        return ExpiresAt is null || now < ExpiresAt;
    }

    /// <summary>
    /// Stops it working, keeping when it was first revoked.
    /// </summary>
    /// <param name="now">The time now, in UTC.</param>
    public void Revoke(DateTime now) => RevokedAt ??= now;

    /// <summary>
    /// Records it being used, unless that was recorded within <see cref="UseRecordedEvery"/>.
    /// </summary>
    /// <param name="now">The time now, in UTC.</param>
    /// <returns>Whether it was recorded, so there's something to save.</returns>
    public bool RecordUse(DateTime now)
    {
        if (LastUsedAt is { } last && now - last < UseRecordedEvery)
        {
            return false;
        }

        LastUsedAt = now;
        return true;
    }
}
