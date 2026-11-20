using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// The <c>Authorization</c> header that proves a push is from this FinTrack, RFC 8292's VAPID. A short-lived JWT
/// signed ES256 (ECDSA P-256 with SHA-256) with <see cref="VapidKeys"/>, and the public key to check it with.
/// </summary>
public static class VapidAuthorization
{
    /// <summary>
    /// The header's scheme.
    /// </summary>
    public const string Scheme = "vapid";

    /// <summary>
    /// How long a token lasts. Push services refuse more than a day.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    /// <summary>
    /// The JWT's header, always the same.
    /// </summary>
    private static readonly string EncodedHeader =
        Base64Url.EncodeToString("""{"typ":"JWT","alg":"ES256"}"""u8);

    /// <summary>
    /// The header's value for a push to <paramref name="endpoint"/>, <c>t=</c> the token and <c>k=</c> the public
    /// key.
    /// </summary>
    /// <param name="endpoint">The browser's push address. The token is for its push service's origin.</param>
    /// <param name="keys">The server's key pair.</param>
    /// <param name="subject">Who runs this FinTrack, the address push services can get in touch through, a
    /// <c>https:</c> or <c>mailto:</c> address. Null leaves it out.</param>
    /// <param name="now">The time now.</param>
    public static string HeaderValue(Uri endpoint, VapidKeys keys, Uri? subject, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(keys);

        return $"t={Token(endpoint, keys, subject, now)}, k={keys.PublicKeyText}";
    }

    /// <summary>
    /// The signed JWT.
    /// </summary>
    /// <param name="endpoint">The browser's push address.</param>
    /// <param name="keys">The server's key pair.</param>
    /// <param name="subject">Who runs this FinTrack, null to leave it out.</param>
    /// <param name="now">The time now.</param>
    internal static string Token(Uri endpoint, VapidKeys keys, Uri? subject, DateTimeOffset now)
    {
        Dictionary<string, object> claims = new()
        {
            ["aud"] = endpoint.GetLeftPart(UriPartial.Authority),
            ["exp"] = (now + Lifetime).ToUnixTimeSeconds()
        };
        if (subject is not null)
        {
            claims["sub"] = subject.ToString();
        }

        string signed = $"{EncodedHeader}.{Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(claims))}";

        // JWT's ES256 is the two 32-byte halves side by side, which is what .NET signs as by default
        using ECDsa key = keys.ToSigningKey();
        byte[] signature = key.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{signed}.{Base64Url.EncodeToString(signature)}";
    }
}
