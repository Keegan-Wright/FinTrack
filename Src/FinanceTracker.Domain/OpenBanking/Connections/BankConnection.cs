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
    private readonly List<string> _scopes = [];
    private readonly List<AccessToken> _accessTokens = [];

    [Obsolete("EF Use Only")]
    private BankConnection()
    {
    }

    public BankConnection(Guid userId, string externalProviderId, string name, string accessCode,
        IEnumerable<string> scopes, byte[] logo) : base(userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(logo);

        ExternalProviderId = externalProviderId;
        Name = name;
        Logo = logo;
        Reauthenticate(accessCode, scopes);
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

    public AccessToken? ActiveToken => _accessTokens.SingleOrDefault(x => !x.IsRevoked);

    public bool HasConsentLapsed(DateTime now, TimeSpan consentLifetime) => now >= ConsentGrantedAt + consentLifetime;

    /// <summary>
    /// Replaces the consent for this connection. Any active token belongs to the old consent and is revoked.
    /// </summary>
    [MemberNotNull(nameof(AccessCode))]
    public void Reauthenticate(string accessCode, IEnumerable<string> scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessCode);
        ArgumentNullException.ThrowIfNull(scopes);

        DateTime now = DateTime.UtcNow;
        AccessCode = accessCode;
        ConsentGrantedAt = now;
        _scopes.Clear();
        _scopes.AddRange(scopes.Distinct());
        ActiveToken?.Revoke(now);
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
