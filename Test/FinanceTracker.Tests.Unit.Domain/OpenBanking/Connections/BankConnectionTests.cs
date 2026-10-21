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
        var before = DateTime.UtcNow;

        var connection = new BankConnection(userId, "ob-bank", "Bank", "access-code", ["accounts", "balance"], logo);

        using (Assert.Multiple())
        {
            await Assert.That(connection.UserId).IsEqualTo(userId);
            await Assert.That(connection.ExternalProviderId).IsEqualTo("ob-bank");
            await Assert.That(connection.Name).IsEqualTo("Bank");
            await Assert.That(connection.AccessCode).IsEqualTo("access-code");
            await Assert.That(connection.Logo).IsEquivalentTo(logo);
            await Assert.That(connection.Scopes).IsEquivalentTo(["accounts", "balance"]);
            await Assert.That(connection.ConsentGrantedAt).IsGreaterThanOrEqualTo(before);
            await Assert.That(connection.AccessTokens.Count).IsEqualTo(0);
            await Assert.That(connection.ActiveToken).IsNull();
        }
    }

    [Test]
    public async Task BankConnection_DuplicateScopes_AreStoredOnce()
    {
        var connection = new BankConnection(Guid.NewGuid(), "ob-bank", "Bank", "code", ["accounts", "accounts", "info"], []);

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
            new BankConnection(Guid.NewGuid(), externalProviderId, name, accessCode, [], []));
    }

    [Test]
    public void BankConnection_NullLogo_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BankConnection(Guid.NewGuid(), "ob-bank", "Bank", "code", [], null!));
    }

    [Test]
    public void BankConnection_NullScopes_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BankConnection(Guid.NewGuid(), "ob-bank", "Bank", "code", null!, []));
    }

    [Test]
    public void BankConnection_EmptyUserId_Throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BankConnection(Guid.Empty, "ob-bank", "Bank", "code", [], []));
    }

    [Test]
    public async Task BankConnection_IssueToken_BecomesActiveToken()
    {
        var connection = BuildConnection();

        var token = connection.IssueToken("token", "refresh", IssuedAt, 3600);

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
        var connection = BuildConnection();
        var first = connection.IssueToken("first", "refresh", IssuedAt, 3600);
        var refreshedAt = IssuedAt.AddMinutes(30);

        var second = connection.IssueToken("second", "refresh", refreshedAt, 3600);

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
        var connection = BuildConnection();

        Assert.Throws<ArgumentException>(() =>
            connection.IssueToken(token, refreshToken, IssuedAt, expiresInSeconds));
    }

    [Test]
    public async Task AccessToken_IsExpired_FromExpiryTime()
    {
        var token = BuildConnection().IssueToken("token", "refresh", IssuedAt, 60);

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
        var connection = BuildConnection();
        var token = connection.IssueToken("token", "refresh", IssuedAt, 3600);
        var revokedAt = IssuedAt.AddDays(1);

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
        var connection = BuildConnection();

        connection.RevokeActiveToken(IssuedAt);

        await Assert.That(connection.AccessTokens.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BankConnection_Reauthenticate_ReplacesConsentAndRevokesActiveToken()
    {
        var connection = BuildConnection();
        var token = connection.IssueToken("token", "refresh", IssuedAt, 3600);
        var originalConsent = connection.ConsentGrantedAt;

        connection.Reauthenticate("new-code", ["transactions", "transactions"]);

        using (Assert.Multiple())
        {
            await Assert.That(connection.AccessCode).IsEqualTo("new-code");
            await Assert.That(connection.Scopes).IsEquivalentTo(["transactions"]);
            await Assert.That(connection.ConsentGrantedAt).IsGreaterThanOrEqualTo(originalConsent);
            await Assert.That(connection.ActiveToken).IsNull();
            await Assert.That(token.IsRevoked).IsTrue();
        }
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public void BankConnection_Reauthenticate_InvalidCode_Throws_ArgumentException(string accessCode)
    {
        var connection = BuildConnection();

        Assert.Throws<ArgumentException>(() =>
            connection.Reauthenticate(accessCode, []));
    }

    [Test]
    public void BankConnection_Reauthenticate_NullScopes_Throws_ArgumentNullException()
    {
        var connection = BuildConnection();

        Assert.Throws<ArgumentNullException>(() =>
            connection.Reauthenticate("code", null!));
    }

    [Test]
    public async Task BankConnection_HasConsentLapsed_FromEndOfLifetime()
    {
        var connection = BuildConnection();
        var lifetime = TimeSpan.FromDays(90);
        var lapsesAt = connection.ConsentGrantedAt + lifetime;

        using (Assert.Multiple())
        {
            await Assert.That(connection.HasConsentLapsed(lapsesAt.AddTicks(-1), lifetime)).IsFalse();
            await Assert.That(connection.HasConsentLapsed(lapsesAt, lifetime)).IsTrue();
        }
    }

    private static BankConnection BuildConnection() =>
        new(Guid.NewGuid(), "ob-bank", "Bank", "code", ["accounts"], [1]);
}
