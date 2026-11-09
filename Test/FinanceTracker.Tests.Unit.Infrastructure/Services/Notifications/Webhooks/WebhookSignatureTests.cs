using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Infrastructure.Notifications.Webhooks;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Webhooks;

public class WebhookSignatureTests
{
    [Test]
    public async Task Sign_Body_IsHmacSha256OfTheTimestampDotBodyInLowercaseHex()
    {
        // Arrange
        byte[] body = """{"id":"1"}"""u8.ToArray();
        string expected = Convert.ToHexStringLower(
            HMACSHA256.HashData("secret"u8.ToArray(), Encoding.UTF8.GetBytes("""1762506000.{"id":"1"}""")));

        // Act
        string signature = WebhookSignature.Sign("secret", 1762506000, body);

        // Assert
        await Assert.That(signature).IsEqualTo($"sha256={expected}");
    }

    [Test]
    public async Task NewSecret_EachTime_IsLongUrlSafeAndDifferent()
    {
        // Act
        string first = WebhookSignature.NewSecret();
        string second = WebhookSignature.NewSecret();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Length).IsEqualTo(43);
            await Assert.That(first).DoesNotContain("+").And.DoesNotContain("/").And.DoesNotContain("=");
            await Assert.That(first).IsNotEqualTo(second);
        }
    }
}
