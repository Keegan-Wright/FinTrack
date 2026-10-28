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

        var connection = new BankConnection(userId, "ob-bank", "Bank", "access-code", ["accounts", "balance"], logo, consentGrantedAt, consentGrantedAt.AddDays(90));

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
        var connection = new BankConnection(Guid.NewGuid(), "ob-bank", "Bank", "code", ["accounts", "accounts", "info"], [], DateTime.UtcNow, DateTime.UtcNow.AddDays(90));

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
            _ = new BankConnection(Guid.NewGuid(), externalProviderId, name, accessCode, [], [], DateTime.UtcNow, DateTime.UtcNow.AddDays(90)));
    }

    [Test]
    public void BankConnection_NullLogo_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _ = new BankConnection(Guid.NewGuid(), "ob-bank", "Bank", "code", [], null!, DateTime.UtcNow, DateTime.UtcNow.AddDays(90)));
    }

    [Test]
    public void BankConnection_NullScopes_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _ = new BankConnection(Guid.NewGuid(), "ob-bank", "Bank", "code", null!, [], DateTime.UtcNow, DateTime.UtcNow.AddDays(90)));
    }

    [Test]
    public void BankConnection_EmptyUserId_Throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new BankConnection(Guid.Empty, "ob-bank", "Bank", "code", [], [], DateTime.UtcNow, DateTime.UtcNow.AddDays(90)));
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

        connection.Reauthenticate("new-code", ["transactions", "transactions"], reauthenticatedAt,
            reauthenticatedAt.AddDays(90));

        using (Assert.Multiple())
        {
            await Assert.That(connection.AccessCode).IsEqualTo("new-code");
            await Assert.That(connection.Scopes).IsEquivalentTo(["transactions"]);
            await Assert.That(connection.ConsentGrantedAt).IsEqualTo(reauthenticatedAt);
            await Assert.That(connection.ConsentExpiresAt).IsEqualTo(reauthenticatedAt.AddDays(90));
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
            connection.Reauthenticate(accessCode, [], DateTime.UtcNow, DateTime.UtcNow.AddDays(90)));
    }

    [Test]
    public void BankConnection_Reauthenticate_NullScopes_Throws_ArgumentNullException()
    {
        BankConnection connection = BuildConnection();

        Assert.Throws<ArgumentNullException>(() =>
            connection.Reauthenticate("code", null!, DateTime.UtcNow, DateTime.UtcNow.AddDays(90)));
    }

    [Test]
    public void BankConnection_Reauthenticate_ExpiryNotAfterGrant_Throws_ArgumentOutOfRangeException()
    {
        BankConnection connection = BuildConnection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            connection.Reauthenticate("code", [], IssuedAt, IssuedAt));
    }

    [Test]
    public async Task BankConnection_HasConsentLapsed_FromTheProvidersExpiry()
    {
        BankConnection connection = BuildConnection();

        using (Assert.Multiple())
        {
            await Assert.That(connection.HasConsentLapsed(ExpiresAt.AddTicks(-1))).IsFalse();
            await Assert.That(connection.HasConsentLapsed(ExpiresAt)).IsTrue();
        }
    }

    [Test]
    public async Task BankConnection_NeedsRenewal_FromTheRenewalWindowBeforeExpiry()
    {
        BankConnection connection = BuildConnection();
        DateTime windowOpens = ExpiresAt - BankConnection.RenewalWindow;

        using (Assert.Multiple())
        {
            await Assert.That(connection.NeedsRenewal(windowOpens.AddTicks(-1))).IsFalse();
            await Assert.That(connection.NeedsRenewal(windowOpens)).IsTrue();
            await Assert.That(connection.NeedsRenewal(ExpiresAt.AddDays(1))).IsTrue();
        }
    }

    [Test]
    public async Task BankConnection_RefreshTokenForRenewal_IsTheLatestEvenOnceRevoked()
    {
        BankConnection connection = BuildConnection();
        connection.IssueToken("first", "first-refresh", IssuedAt, 3600);
        connection.IssueToken("second", "second-refresh", IssuedAt.AddHours(1), 3600);
        connection.RevokeActiveToken(ExpiresAt);

        await Assert.That(connection.RefreshTokenForRenewal()).IsEqualTo("second-refresh");
    }

    [Test]
    public void BankConnection_RefreshTokenForRenewal_NoTokens_Throws_InvalidOperationException()
    {
        BankConnection connection = BuildConnection();

        Assert.Throws<InvalidOperationException>(() => connection.RefreshTokenForRenewal());
    }

    private static DateTime ExpiresAt => IssuedAt.AddDays(90);

    private static BankConnection BuildConnection() =>
        new(Guid.NewGuid(), "ob-bank", "Bank", "code", ["accounts"], [1], IssuedAt, ExpiresAt);
}
