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

    public string Token { get; private set; }

    public string RefreshToken { get; private set; }

    public DateTime IssuedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>
    /// The access token has lapsed and must be refreshed before use
    /// </summary>
    public bool IsExpired(DateTime now) => now >= ExpiresAt;

    internal void Revoke(DateTime revokedAt) => RevokedAt ??= revokedAt;
}
