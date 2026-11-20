using FinanceTracker.Domain.Identity;

namespace FinanceTracker.Tests.Unit.Domain.Identity;

public class PersonalAccessTokenTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task IsActive_BeforeItExpires_IsTrue()
    {
        // Arrange
        PersonalAccessToken token = Token(expiresAt: Now.AddDays(1));

        // Act & Assert
        await Assert.That(token.IsActive(Now)).IsTrue();
    }

    [Test]
    public async Task IsActive_OnceExpired_IsFalse()
    {
        // Arrange
        PersonalAccessToken token = Token(expiresAt: Now);

        // Act & Assert
        await Assert.That(token.IsActive(Now)).IsFalse();
    }

    [Test]
    public async Task IsActive_NeverExpiring_IsTrueUntilRevoked()
    {
        // Arrange
        PersonalAccessToken token = Token(expiresAt: null);
        bool before = token.IsActive(Now.AddYears(10));

        // Act
        token.Revoke(Now);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(before).IsTrue();
            await Assert.That(token.IsActive(Now.AddYears(10))).IsFalse();
        }
    }

    [Test]
    public async Task Revoke_Twice_KeepsTheFirstTime()
    {
        // Arrange
        PersonalAccessToken token = Token(expiresAt: null);

        // Act
        token.Revoke(Now);
        token.Revoke(Now.AddDays(1));

        // Assert
        await Assert.That(token.RevokedAt).IsEqualTo(Now);
    }

    [Test]
    public async Task RecordUse_WithinTheInterval_IsRecordedOnce()
    {
        // Arrange
        PersonalAccessToken token = Token(expiresAt: null);

        // Act
        bool first = token.RecordUse(Now);
        bool soonAfter = token.RecordUse(Now.AddSeconds(30));
        bool later = token.RecordUse(Now.Add(PersonalAccessToken.UseRecordedEvery));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first).IsTrue();
            await Assert.That(soonAfter).IsFalse();
            await Assert.That(later).IsTrue();
            await Assert.That(token.LastUsedAt).IsEqualTo(Now.Add(PersonalAccessToken.UseRecordedEvery));
        }
    }

    [Test]
    public async Task New_IsReadOnly()
    {
        // Act & Assert
        await Assert.That(Token(expiresAt: null).Scope).IsEqualTo(TokenScope.Read);
    }

    private static PersonalAccessToken Token(DateTime? expiresAt) =>
        new(Guid.NewGuid(), " Home Assistant ", "0123456789abcdef", [1, 2, 3], expiresAt);
}
