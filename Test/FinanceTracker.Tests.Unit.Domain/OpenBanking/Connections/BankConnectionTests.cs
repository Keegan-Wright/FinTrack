using FinanceTracker.Domain.OpenBanking.Connections;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Connections;

[Category("Domain")]
public class BankConnectionTests
{
    private static readonly DateTime IssuedAt = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task BankConnection_ValidConstruction_SetsProperties()
    {
        var userId = Guid.NewGuid();
        byte[] logo = [1, 2, 3];
        DateTime consentGrantedAt = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        var connection = new BankConnection(userId, "ob-bank", "Bank", "access-code", ["accounts", "balance"], logo, consentGrantedAt);

        using (Assert.Multiple())
        {
            await Assert.That(connection.UserId).IsEqualTo(userId);
            await Assert.That(connection.ExternalProviderId).IsEqualTo("ob-bank");
            await Assert.That(connection.Name).IsEqualTo("Bank");
            await Assert.That(connection.AccessCode).IsEqualTo("access-code");
            await Assert.That(connection.Logo).IsEquivalentTo(logo);
            await Assert.That(connection.Scopes).IsEquivalentTo(["accounts", "balance"]);
            await Assert.That(connection.ConsentGrantedAt).IsEqualTo(consentGrantedAt);
            await Assert.That(connection.AccessTokens.Count).IsEqualTo(0);
            await Assert.That(connection.ActiveToken).IsNull();
        }
    }

    [Test]
    public async Task BankConnection_DuplicateScopes_AreStoredOnce()
    {
        var connection = new BankConnection(Guid.NewGuid(), "ob-bank", "Bank", "code", ["accounts", "accounts", "info"], [], DateTime.UtcNow);

        await Assert.That(connection.Scopes).IsEquivalentTo(["accounts", "info"]);
    }

    [Test]
    [Arguments("", "Bank", "code")]
    [Arguments(" ", "Bank", "code")]
    [Arguments("ob-bank", "", "code")]
    [Arguments("ob-bank", "Bank", "")]
    public void BankConnection_InvalidConstruction_Throws_ArgumentException(string externalProviderId, string name,
        string accessCode)
    {
        Assert.Throws<ArgumentException>(() =>
            _ = new BankConnection(Guid.NewGuid(), externalProviderId, name, accessCode, [], [], DateTime.UtcNow));
    }

    [Test]
    public void BankConnection_NullLogo_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _ = new BankConnection(Guid.NewGuid(), "ob-bank", "Bank", "code", [], null!, DateTime.UtcNow));
    }

    [Test]
    public void BankConnection_NullScopes_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _ = new BankConnection(Guid.NewGuid(), "ob-bank", "Bank", "code", null!, [], DateTime.UtcNow));
    }

    [Test]
    public void BankConnection_EmptyUserId_Throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new BankConnection(Guid.Empty, "ob-bank", "Bank", "code", [], [], DateTime.UtcNow));
    }

    [Test]
    public async Task BankConnection_IssueToken_BecomesActiveToken()
    {
        BankConnection connection = BuildConnection();

        AccessToken token = connection.IssueToken("token", "refresh", IssuedAt, 3600);

        using (Assert.Multiple())
        {
            await Assert.That(connection.ActiveToken).IsEqualTo(token);
            await Assert.That(connection.AccessTokens.Count).IsEqualTo(1);
            await Assert.That(token.Token).IsEqualTo("token");
            await Assert.That(token.RefreshToken).IsEqualTo("refresh");
            await Assert.That(token.IssuedAt).IsEqualTo(IssuedAt);
            await Assert.That(token.ExpiresAt).IsEqualTo(IssuedAt.AddHours(1));
            await Assert.That(token.IsRevoked).IsFalse();
            await Assert.That(token.RevokedAt).IsNull();
        }
    }

    [Test]
    public async Task BankConnection_IssueToken_RevokesPreviousTokenAtIssueTime()
    {
        BankConnection connection = BuildConnection();
        AccessToken first = connection.IssueToken("first", "refresh", IssuedAt, 3600);
        DateTime refreshedAt = IssuedAt.AddMinutes(30);

        AccessToken second = connection.IssueToken("second", "refresh", refreshedAt, 3600);

        using (Assert.Multiple())
        {
            await Assert.That(connection.ActiveToken).IsEqualTo(second);
            await Assert.That(connection.AccessTokens.Count).IsEqualTo(2);
            await Assert.That(first.IsRevoked).IsTrue();
            await Assert.That(first.RevokedAt).IsEqualTo(refreshedAt);
        }
    }

    [Test]
    [Arguments("", "refresh", 3600)]
    [Arguments("token", "", 3600)]
    [Arguments("token", "refresh", 0)]
    [Arguments("token", "refresh", -1)]
    public void BankConnection_IssueToken_Invalid_Throws_ArgumentException(string token, string refreshToken,
        int expiresInSeconds)
    {
        BankConnection connection = BuildConnection();

        Assert.Throws<ArgumentException>(() =>
            connection.IssueToken(token, refreshToken, IssuedAt, expiresInSeconds));
    }

    [Test]
    public async Task AccessToken_IsExpired_FromExpiryTime()
    {
        AccessToken token = BuildConnection().IssueToken("token", "refresh", IssuedAt, 60);

        using (Assert.Multiple())
        {
            await Assert.That(token.IsExpired(IssuedAt.AddSeconds(59))).IsFalse();
            await Assert.That(token.IsExpired(IssuedAt.AddSeconds(60))).IsTrue();
            await Assert.That(token.IsExpired(IssuedAt.AddSeconds(61))).IsTrue();
        }
    }

    [Test]
    public async Task BankConnection_RevokeActiveToken_LeavesNoActiveToken()
    {
        BankConnection connection = BuildConnection();
        AccessToken token = connection.IssueToken("token", "refresh", IssuedAt, 3600);
        DateTime revokedAt = IssuedAt.AddDays(1);

        connection.RevokeActiveToken(revokedAt);

        using (Assert.Multiple())
        {
            await Assert.That(connection.ActiveToken).IsNull();
            await Assert.That(token.RevokedAt).IsEqualTo(revokedAt);
        }
    }

    [Test]
    public async Task BankConnection_RevokeActiveToken_WithoutToken_DoesNothing()
    {
        BankConnection connection = BuildConnection();

        connection.RevokeActiveToken(IssuedAt);

        await Assert.That(connection.AccessTokens.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BankConnection_Reauthenticate_ReplacesConsentAndRevokesActiveToken()
    {
        BankConnection connection = BuildConnection();
        AccessToken token = connection.IssueToken("token", "refresh", IssuedAt, 3600);
        DateTime reauthenticatedAt = IssuedAt.AddHours(1);

        connection.Reauthenticate("new-code", ["transactions", "transactions"], reauthenticatedAt);

        using (Assert.Multiple())
        {
            await Assert.That(connection.AccessCode).IsEqualTo("new-code");
            await Assert.That(connection.Scopes).IsEquivalentTo(["transactions"]);
            await Assert.That(connection.ConsentGrantedAt).IsEqualTo(reauthenticatedAt);
            await Assert.That(connection.ActiveToken).IsNull();
            await Assert.That(token.RevokedAt).IsEqualTo(reauthenticatedAt);
        }
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public void BankConnection_Reauthenticate_InvalidCode_Throws_ArgumentException(string accessCode)
    {
        BankConnection connection = BuildConnection();

        Assert.Throws<ArgumentException>(() =>
            connection.Reauthenticate(accessCode, [], DateTime.UtcNow));
    }

    [Test]
    public void BankConnection_Reauthenticate_NullScopes_Throws_ArgumentNullException()
    {
        BankConnection connection = BuildConnection();

        Assert.Throws<ArgumentNullException>(() =>
            connection.Reauthenticate("code", null!, DateTime.UtcNow));
    }

    [Test]
    public async Task BankConnection_HasConsentLapsed_FromEndOfLifetime()
    {
        BankConnection connection = BuildConnection();
        var lifetime = TimeSpan.FromDays(90);
        DateTime lapsesAt = connection.ConsentGrantedAt + lifetime;

        using (Assert.Multiple())
        {
            await Assert.That(connection.HasConsentLapsed(lapsesAt.AddTicks(-1), lifetime)).IsFalse();
            await Assert.That(connection.HasConsentLapsed(lapsesAt, lifetime)).IsTrue();
        }
    }

    private static BankConnection BuildConnection() =>
        new(Guid.NewGuid(), "ob-bank", "Bank", "code", ["accounts"], [1], IssuedAt);
}
