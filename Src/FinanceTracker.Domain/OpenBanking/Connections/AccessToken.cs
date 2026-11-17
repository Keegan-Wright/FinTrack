using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.OpenBanking.Connections;

/// <summary>
/// A single token issued for a <see cref="BankConnection"/>.
/// </summary>
public sealed class AccessToken : Entity
{
    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private AccessToken()
    {
    }
#pragma warning restore CS8618

    /// <param name="token">The access token.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="issuedAt">When it was issued.</param>
    /// <param name="expiresInSeconds">How long the access token lasts.</param>
    internal AccessToken(string token, string refreshToken, DateTime issuedAt, int expiresInSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expiresInSeconds);

        Token = token;
        RefreshToken = refreshToken;
        IssuedAt = issuedAt;
        ExpiresAt = issuedAt.AddSeconds(expiresInSeconds);
    }

    /// <summary>
    /// The access token, sent with each call to the provider. Encrypted at rest.
    /// </summary>
    public string Token { get; private set; }

    /// <summary>
    /// The token that gets a new access token once this one expires. Encrypted at rest.
    /// </summary>
    public string RefreshToken { get; private set; }

    /// <summary>
    /// When it was issued.
    /// </summary>
    public DateTime IssuedAt { get; private set; }

    /// <summary>
    /// When the access token stops working.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// When it stopped being used, null while it's the one in use.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// Whether it has stopped being used.
    /// </summary>
    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>
    /// The access token has lapsed and must be refreshed before use
    /// </summary>
    /// <param name="now">The time now, in UTC.</param>
    public bool IsExpired(DateTime now) => now >= ExpiresAt;

    /// <summary>
    /// Stops it being used, keeping when it first stopped.
    /// </summary>
    /// <param name="revokedAt">When it stopped.</param>
    internal void Revoke(DateTime revokedAt) => RevokedAt ??= revokedAt;
}
