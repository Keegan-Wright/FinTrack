using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.OpenBanking.Connections;

/// <summary>
/// A user's authorised link to a single open banking provider.
/// Holds the consented access code and scopes and the full history of issued access tokens.
/// </summary>
/// <remarks>
/// Token history grows with every refresh, load only the active token where possible
/// <c>Include(x => x.AccessTokens.Where(t => t.RevokedAt == null))</c>.
/// </remarks>
public sealed class BankConnection : AggregateRoot
{
    /// <summary>
    /// How long before consent ends the user is asked to renew it.
    /// </summary>
    public static readonly TimeSpan RenewalWindow = TimeSpan.FromDays(14);

    private readonly List<string> _scopes = [];
    private readonly List<AccessToken> _accessTokens = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private BankConnection()
    {
    }
#pragma warning restore CS8618

    public BankConnection(Guid userId, string externalProviderId, string name, string accessCode,
        IEnumerable<string> scopes, byte[] logo, DateTime consentGrantedAt, DateTime consentExpiresAt) : base(userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(logo);

        ExternalProviderId = externalProviderId;
        Name = name;
        Logo = logo;
        Reauthenticate(accessCode, scopes, consentGrantedAt, consentExpiresAt);
    }

    public string ExternalProviderId { get; private set; }

    public string Name { get; private set; }

    public string AccessCode { get; private set; }

    public byte[] Logo { get; private set; }

    public IReadOnlyCollection<string> Scopes => _scopes.AsReadOnly();

    public IReadOnlyCollection<AccessToken> AccessTokens => _accessTokens.AsReadOnly();

    /// <summary>
    /// When the user last granted consent.
    /// </summary>
    public DateTime ConsentGrantedAt { get; private set; }

    /// <summary>
    /// When the consent ends, as the provider reports it. The user has to renew it before then to keep syncing.
    /// </summary>
    public DateTime ConsentExpiresAt { get; private set; }

    public AccessToken? ActiveToken => _accessTokens.SingleOrDefault(x => !x.IsRevoked);

    public bool HasConsentLapsed(DateTime now) => now >= ConsentExpiresAt;

    /// <summary>
    /// Consent ends within <see cref="RenewalWindow"/>, or already has, so the user should renew it.
    /// </summary>
    public bool NeedsRenewal(DateTime now) => now >= ConsentExpiresAt - RenewalWindow;

    /// <summary>
    /// The refresh token to renew the consent with: the latest one issued, revoked or not, as renewing works for a
    /// while after consent has lapsed. Needs the latest token loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">No token has been issued for the connection.</exception>
    public string RefreshTokenForRenewal()
    {
        AccessToken? latest = _accessTokens.MaxBy(static x => x.IssuedAt);
        if (latest is null)
        {
            throw new InvalidOperationException($"Connection {Id} has no token to renew with.");
        }

        return latest.RefreshToken;
    }

    /// <summary>
    /// Replaces the consent for this connection. Any active token belongs to the old consent and is revoked.
    /// </summary>
    [MemberNotNull(nameof(AccessCode))]
    public void Reauthenticate(string accessCode, IEnumerable<string> scopes, DateTime reauthenticatedAt,
        DateTime consentExpiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessCode);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(consentExpiresAt, reauthenticatedAt);

        AccessCode = accessCode;
        ConsentGrantedAt = reauthenticatedAt;
        ConsentExpiresAt = consentExpiresAt;
        _scopes.Clear();
        _scopes.AddRange(scopes.Distinct());
        ActiveToken?.Revoke(reauthenticatedAt);
    }

    /// <summary>
    /// Records a newly issued token (code exchange or refresh), revoking the previously active one.
    /// </summary>
    public AccessToken IssueToken(string accessToken, string refreshToken, DateTime issuedAt, int expiresInSeconds)
    {
        ActiveToken?.Revoke(issuedAt);

        AccessToken token = new(accessToken, refreshToken, issuedAt, expiresInSeconds);
        _accessTokens.Add(token);
        return token;
    }

    /// <summary>
    /// Ends the current consent, e.g. when the banking provider's consent window has elapsed.
    /// </summary>
    public void RevokeActiveToken(DateTime revokedAt) => ActiveToken?.Revoke(revokedAt);
}
