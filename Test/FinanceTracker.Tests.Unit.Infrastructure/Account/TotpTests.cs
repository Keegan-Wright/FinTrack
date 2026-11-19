using System.Text;
using FinanceTracker.Infrastructure.Account.Authenticator;

namespace FinanceTracker.Tests.Unit.Infrastructure.Account;

public class TotpTests
{
    /// <summary>
    /// The SHA-256 seed from RFC 6238 appendix B.
    /// </summary>
    private static readonly byte[] RfcSeed = Encoding.ASCII.GetBytes("12345678901234567890123456789012");

    [Test]
    [Arguments(59L, "46119246")]
    [Arguments(1111111109L, "68084774")]
    [Arguments(1111111111L, "67062674")]
    [Arguments(1234567890L, "91819424")]
    [Arguments(2000000000L, "90698825")]
    [Arguments(20000000000L, "77737706")]
    public async Task Code_Rfc6238Sha256TestVectors_Match(long unixSeconds, string expected)
    {
        // Arrange
        long step = Totp.StepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

        // Act
        string code = Totp.Code(RfcSeed, step, digits: 8);

        // Assert
        await Assert.That(code).IsEqualTo(expected);
    }

    [Test]
    public async Task Code_DefaultDigits_IsTheLastSixOfTheRfcVector()
    {
        // Act
        string code = Totp.Code(RfcSeed, Totp.StepAt(DateTimeOffset.FromUnixTimeSeconds(59)));

        // Assert
        await Assert.That(code).IsEqualTo("119246");
    }

    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(1)]
    public async Task MatchingStep_WithinTheAllowedDrift_GivesTheCodesStep(int drift)
    {
        // Arrange
        const long now = 1000;
        string code = Totp.Code(RfcSeed, now + drift);

        // Act
        long? matched = Totp.MatchingStep(RfcSeed, code, now);

        // Assert
        await Assert.That(matched).IsEqualTo(now + drift);
    }

    [Test]
    [Arguments(-2)]
    [Arguments(2)]
    public async Task MatchingStep_OutsideTheAllowedDrift_IsNull(int drift)
    {
        // Arrange
        const long now = 1000;
        string code = Totp.Code(RfcSeed, now + drift);

        // Act
        long? matched = Totp.MatchingStep(RfcSeed, code, now);

        // Assert
        await Assert.That(matched).IsNull();
    }

    [Test]
    [Arguments("")]
    [Arguments("12345")]
    [Arguments("1234567")]
    [Arguments("12345a")]
    public async Task MatchingStep_NotSixDigits_IsNull(string code)
    {
        // Act & Assert
        await Assert.That(Totp.MatchingStep(RfcSeed, code, 1000)).IsNull();
    }
}
