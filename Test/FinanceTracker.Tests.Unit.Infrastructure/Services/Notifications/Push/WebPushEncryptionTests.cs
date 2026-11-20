using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Infrastructure.Notifications.Push;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Push;

/// <summary>
/// The push encryption, against RFC 8291's example in section 5 and appendix A.
/// </summary>
public class WebPushEncryptionTests
{
    private const string RfcPlaintext = "When I grow up, I want to be a watermelon";

    private const string RfcBody =
        "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27ml" +
        "mlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPT" +
        "pK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN";

    [Test]
    public async Task Encrypt_TheRfcExample_GivesTheRfcBody()
    {
        // Arrange, the RFC's server key pair and salt
        using WebPushTestBrowser browser = WebPushTestBrowser.RfcExample();
        byte[] serverPublicKey = Base64Url.DecodeFromChars(
            "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8");
        using ECDiffieHellman serverKey = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = serverPublicKey[1..33], Y = serverPublicKey[33..] },
            D = Base64Url.DecodeFromChars("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw")
        });
        byte[] salt = Base64Url.DecodeFromChars("DGv6ra1nlYgDCS1FRnbzlw");

        // Act
        byte[] body = WebPushEncryption.Encrypt(Encoding.ASCII.GetBytes(RfcPlaintext), browser.PublicKey,
            browser.AuthSecret, serverKey, salt);

        // Assert
        await Assert.That(Base64Url.EncodeToString(body)).IsEqualTo(RfcBody);
    }

    [Test]
    public async Task TestBrowser_TheRfcBody_ReadsTheRfcPlaintext()
    {
        // Arrange, checks the test browser itself against the RFC
        using WebPushTestBrowser browser = WebPushTestBrowser.RfcExample();

        // Act
        byte[] plaintext = browser.Decrypt(Base64Url.DecodeFromChars(RfcBody));

        // Assert
        await Assert.That(Encoding.ASCII.GetString(plaintext)).IsEqualTo(RfcPlaintext);
    }

    [Test]
    public async Task Encrypt_SameMessageTwice_DiffersEachTime_AndTheBrowserReadsBoth()
    {
        // Arrange
        using WebPushTestBrowser browser = new();
        byte[] message = Encoding.UTF8.GetBytes("""{"title":"FinTrack","body":"Budget alert"}""");

        // Act
        byte[] first = WebPushEncryption.Encrypt(message, browser.PublicKey, browser.AuthSecret);
        byte[] second = WebPushEncryption.Encrypt(message, browser.PublicKey, browser.AuthSecret);

        // Assert, a fresh key and salt each time
        using (Assert.Multiple())
        {
            await Assert.That(first.SequenceEqual(second)).IsFalse();
            await Assert.That(browser.Decrypt(first)).IsEquivalentTo(message);
            await Assert.That(browser.Decrypt(second)).IsEquivalentTo(message);
        }
    }

    [Test]
    public async Task Encrypt_ForAnotherBrowser_ThisOneCantReadIt()
    {
        // Arrange
        using WebPushTestBrowser intended = new();
        using WebPushTestBrowser other = new();

        // Act
        byte[] body = WebPushEncryption.Encrypt("secret"u8, intended.PublicKey, intended.AuthSecret);

        // Assert
        await Assert.That(() => other.Decrypt(body)).Throws<CryptographicException>();
    }

    [Test]
    public async Task Encrypt_TheMostThatFits_FillsOneRecord()
    {
        // Arrange
        using WebPushTestBrowser browser = new();
        byte[] message = new byte[WebPushEncryption.MostPlaintextBytes];

        // Act
        byte[] body = WebPushEncryption.Encrypt(message, browser.PublicKey, browser.AuthSecret);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(body.Length).IsEqualTo(WebPushEncryption.RecordSize);
            await Assert.That(browser.Decrypt(body)).IsEquivalentTo(message);
        }
    }

    [Test]
    public async Task Encrypt_OneByteTooLong_IsRefused()
    {
        // Arrange
        using WebPushTestBrowser browser = new();
        byte[] message = new byte[WebPushEncryption.MostPlaintextBytes + 1];

        // Act, Assert
        await Assert.That(() => WebPushEncryption.Encrypt(message, browser.PublicKey, browser.AuthSecret))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Encrypt_ShortAuthSecret_IsRefused()
    {
        // Arrange
        using WebPushTestBrowser browser = new();

        // Act, Assert
        await Assert.That(() => WebPushEncryption.Encrypt("hi"u8, browser.PublicKey, new byte[8]))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Encrypt_KeyNotOnTheCurve_IsRefused()
    {
        // Arrange, the right length but not a point on P-256
        byte[] notAPoint = [0x04, .. new byte[64]];

        // Act, Assert
        await Assert.That(() => WebPushEncryption.Encrypt("hi"u8, notAPoint, new byte[16]))
            .Throws<CryptographicException>();
    }
}
