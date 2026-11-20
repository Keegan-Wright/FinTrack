using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinanceTracker.Infrastructure.Notifications.Push;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Push;

/// <summary>
/// The VAPID header, as a push service checks it, RFC 8292.
/// </summary>
public class VapidAuthorizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly Uri Endpoint = new("https://push.example.test/send/abc123?x=1");
    private static readonly Uri Subject = new("https://fintrack.example.test/");

    [Test]
    public async Task HeaderValue_GivesTheTokenAndThePublicKey()
    {
        // Arrange
        VapidKeys keys = VapidKeys.Create();

        // Act
        string header = VapidAuthorization.HeaderValue(Endpoint, keys, Subject, Now);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(header).StartsWith("t=");
            await Assert.That(header).EndsWith($", k={keys.PublicKeyText}");
        }
    }

    [Test]
    public async Task Token_IsForThePushServicesOrigin_ExpiresInTwelveHours_SaysWhoSends()
    {
        // Arrange
        VapidKeys keys = VapidKeys.Create();

        // Act
        string[] parts = VapidAuthorization.Token(Endpoint, keys, Subject, Now).Split('.');

        // Assert
        using JsonDocument header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
        using JsonDocument claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        using (Assert.Multiple())
        {
            await Assert.That(header.RootElement.GetProperty("alg").GetString()).IsEqualTo("ES256");
            await Assert.That(header.RootElement.GetProperty("typ").GetString()).IsEqualTo("JWT");
            await Assert.That(claims.RootElement.GetProperty("aud").GetString()).IsEqualTo("https://push.example.test");
            await Assert.That(claims.RootElement.GetProperty("exp").GetInt64())
                .IsEqualTo(Now.AddHours(12).ToUnixTimeSeconds());
            await Assert.That(claims.RootElement.GetProperty("sub").GetString())
                .IsEqualTo("https://fintrack.example.test/");
        }
    }

    [Test]
    public async Task Token_WithoutASubject_LeavesItOut()
    {
        // Arrange
        VapidKeys keys = VapidKeys.Create();

        // Act
        string[] parts = VapidAuthorization.Token(Endpoint, keys, null, Now).Split('.');

        // Assert
        using JsonDocument claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        await Assert.That(claims.RootElement.TryGetProperty("sub", out _)).IsFalse();
    }

    [Test]
    public async Task Token_SignatureChecksOutWithThePublicKey_AndNotAnother()
    {
        // Arrange
        VapidKeys keys = VapidKeys.Create();
        VapidKeys other = VapidKeys.Create();

        // Act
        string token = VapidAuthorization.Token(Endpoint, keys, Subject, Now);

        // Assert, as a push service checks it, the signed part and a 64 byte ES256 signature
        int lastDot = token.LastIndexOf('.');
        byte[] signed = Encoding.ASCII.GetBytes(token[..lastDot]);
        byte[] signature = Base64Url.DecodeFromChars(token.AsSpan(lastDot + 1));
        using (Assert.Multiple())
        {
            await Assert.That(signature.Length).IsEqualTo(64);
            await Assert.That(Verifies(keys, signed, signature)).IsTrue();
            await Assert.That(Verifies(other, signed, signature)).IsFalse();
        }
    }

    [Test]
    public async Task VapidKeys_WrongLengths_AreRefused()
    {
        // Arrange
        VapidKeys keys = VapidKeys.Create();

        // Act, Assert
        using (Assert.Multiple())
        {
            await Assert.That(() => new VapidKeys(keys.PublicKey[..64], keys.PrivateKey))
                .Throws<ArgumentException>();
            await Assert.That(() => new VapidKeys(keys.PublicKey, keys.PrivateKey[..31]))
                .Throws<ArgumentException>();
        }
    }

    private static bool Verifies(VapidKeys keys, byte[] signed, byte[] signature)
    {
        using ECDsa publicOnly = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = keys.PublicKey[1..33], Y = keys.PublicKey[33..] }
        });
        return publicOnly.VerifyData(signed, signature, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }
}
