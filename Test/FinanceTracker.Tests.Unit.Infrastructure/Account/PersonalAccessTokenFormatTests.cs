using System.Text.RegularExpressions;
using FinanceTracker.Infrastructure.Account.Tokens;

namespace FinanceTracker.Tests.Unit.Infrastructure.Account;

public partial class PersonalAccessTokenFormatTests
{
    [Test]
    public async Task Issue_MakesAPrefixedTokenWithAHexIdAnd256BitSecret()
    {
        // Act
        IssuedToken issued = PersonalAccessTokenFormat.Issue();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(TokenShape().IsMatch(issued.Token)).IsTrue();
            await Assert.That(issued.Token).StartsWith($"ftk_{issued.LookupId}_");
            await Assert.That(issued.SecretHash.Length).IsEqualTo(32);
        }
    }

    [Test]
    public async Task Issue_TwiceInARow_GivesDifferentTokens()
    {
        // Act
        IssuedToken first = PersonalAccessTokenFormat.Issue();
        IssuedToken second = PersonalAccessTokenFormat.Issue();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.LookupId).IsNotEqualTo(second.LookupId);
            await Assert.That(first.SecretHash).IsNotEquivalentTo(second.SecretHash);
        }
    }

    [Test]
    public async Task TryRead_AnIssuedToken_GivesItsIdAndAMatchingHash()
    {
        // Arrange
        IssuedToken issued = PersonalAccessTokenFormat.Issue();

        // Act
        bool read = PersonalAccessTokenFormat.TryRead(issued.Token, out string? lookupId, out byte[]? secretHash);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(read).IsTrue();
            await Assert.That(lookupId).IsEqualTo(issued.LookupId);
            await Assert.That(PersonalAccessTokenFormat.Matches(secretHash!, issued.SecretHash)).IsTrue();
        }
    }

    [Test]
    public async Task Matches_AnotherTokensSecret_IsFalse()
    {
        // Arrange
        IssuedToken issued = PersonalAccessTokenFormat.Issue();
        IssuedToken other = PersonalAccessTokenFormat.Issue();
        string forged = $"ftk_{issued.LookupId}_{other.Token[(other.Token.LastIndexOf('_') + 1)..]}";

        // Act
        PersonalAccessTokenFormat.TryRead(forged, out _, out byte[]? secretHash);

        // Assert
        await Assert.That(PersonalAccessTokenFormat.Matches(secretHash!, issued.SecretHash)).IsFalse();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("ghp_0123456789abcdef_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [Arguments("ftk_0123456789ABCDEF_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [Arguments("ftk_0123456789abcdef-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [Arguments("ftk_0123456789abcdef_AAAA")]
    [Arguments("ftk_0123456789abcdef_!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    [Arguments("ftk_0123456789abcdef_")]
    public async Task TryRead_NotAToken_IsFalse(string? token)
    {
        // Act & Assert
        await Assert.That(PersonalAccessTokenFormat.TryRead(token, out _, out _)).IsFalse();
    }

    [GeneratedRegex("^ftk_[0-9a-f]{16}_[A-Za-z0-9_-]{43}$")]
    private static partial Regex TokenShape();
}
