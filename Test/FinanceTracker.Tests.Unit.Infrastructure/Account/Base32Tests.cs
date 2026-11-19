using System.Text;
using FinanceTracker.Infrastructure.Account.Authenticator;

namespace FinanceTracker.Tests.Unit.Infrastructure.Account;

public class Base32Tests
{
    [Test]
    [Arguments("", "")]
    [Arguments("f", "MY")]
    [Arguments("fo", "MZXQ")]
    [Arguments("foo", "MZXW6")]
    [Arguments("foob", "MZXW6YQ")]
    [Arguments("fooba", "MZXW6YTB")]
    [Arguments("foobar", "MZXW6YTBOI")]
    public async Task Encode_Rfc4648TestVectors_MatchWithoutPadding(string text, string expected)
    {
        // Act
        string encoded = Base32.Encode(Encoding.ASCII.GetBytes(text));

        // Assert
        await Assert.That(encoded).IsEqualTo(expected);
    }

    [Test]
    [Arguments("MZXW6YTBOI", "foobar")]
    [Arguments("MZXW6YTBOI======", "foobar")]
    [Arguments("mzxw 6ytb oi", "foobar")]
    public async Task Decode_AsPeopleTypeIt_GivesTheBytes(string text, string expected)
    {
        // Act
        byte[] decoded = Base32.Decode(text);

        // Assert
        await Assert.That(Encoding.ASCII.GetString(decoded)).IsEqualTo(expected);
    }

    [Test]
    public async Task Decode_ACharacterBase32DoesNotUse_Throws()
    {
        // Act & Assert
        await Assert.That(() => Base32.Decode("MZXW1")).Throws<FormatException>();
    }

    [Test]
    public async Task EncodeThenDecode_AnAuthenticatorKey_RoundTrips()
    {
        // Arrange
        byte[] key = new byte[AuthenticatorSettings.KeyBytes];
        Random.Shared.NextBytes(key);

        // Act
        byte[] decoded = Base32.Decode(Base32.Encode(key));

        // Assert
        await Assert.That(decoded).IsEquivalentTo(key);
    }
}
