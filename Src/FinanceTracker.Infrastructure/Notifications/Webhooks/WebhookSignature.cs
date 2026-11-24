using System.Buffers.Text;
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
    /// <summary>
    /// How many random bytes a secret has.
    /// </summary>
    private const int SecretBytes = 32;

    /// <summary>
    /// A new secret, 32 random bytes as URL safe base 64.
    /// </summary>
    public static string NewSecret()
    {
        byte[] secret = RandomNumberGenerator.GetBytes(SecretBytes);
        try
        {
            return Base64Url.EncodeToString(secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <param name="secret">The webhook's secret.</param>
    /// <param name="timestamp">When it's sent, in Unix seconds.</param>
    /// <param name="body">The post's body.</param>
    /// <returns>The header's value, <c>sha256={lowercase hex}</c>.</returns>
    public static string Sign(string secret, long timestamp, ReadOnlySpan<byte> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        byte[] prefix = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".");
        byte[] signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed.AsSpan(prefix.Length));

        byte[] key = Encoding.UTF8.GetBytes(secret);
        try
        {
            return "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(key, signed));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
