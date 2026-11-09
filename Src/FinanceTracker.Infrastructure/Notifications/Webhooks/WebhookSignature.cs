using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// How a webhook post is signed, so whatever receives it can check it came from FinTrack and wasn't replayed.
/// HMAC-SHA256 keyed with the secret (its UTF-8 bytes) over <c>{timestamp}.{body}</c>, sent as
/// <c>X-FinTrack-Signature sha256={lowercase hex}</c> with the timestamp, in Unix seconds, as
/// <c>X-FinTrack-Timestamp</c>.
/// </summary>
public static class WebhookSignature
{
    private const int SecretBytes = 32;

    /// <summary>
    /// A new secret, 32 random bytes as URL safe base 64.
    /// </summary>
    public static string NewSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <returns>The header's value, <c>sha256={lowercase hex}</c>.</returns>
    public static string Sign(string secret, long timestamp, ReadOnlySpan<byte> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        byte[] prefix = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".");
        byte[] signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed.AsSpan(prefix.Length));

        byte[] hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);
        return "sha256=" + Convert.ToHexStringLower(hash);
    }
}
