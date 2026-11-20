using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace FinanceTracker.Tests.Unit.Shared;

/// <summary>
/// Stands in for a browser subscribed to push. Its key pair and auth secret, and reading a push the way a browser
/// does, RFC 8291's receiving side. Written from the RFC apart from the app's code, so the two check each other.
/// </summary>
public sealed class WebPushTestBrowser : IDisposable
{
    private readonly ECDiffieHellman _key;

    /// <summary>
    /// A browser with a new key pair and secret.
    /// </summary>
    public WebPushTestBrowser() : this(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256),
        RandomNumberGenerator.GetBytes(16))
    {
    }

    /// <param name="key">Its key pair.</param>
    /// <param name="authSecret">Its auth secret.</param>
    private WebPushTestBrowser(ECDiffieHellman key, byte[] authSecret)
    {
        _key = key;
        AuthSecret = authSecret;
        ECParameters parameters = key.ExportParameters(includePrivateParameters: false);
        PublicKey = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
    }

    /// <summary>
    /// Its <c>p256dh</c> key, uncompressed.
    /// </summary>
    public byte[] PublicKey { get; }

    /// <summary>
    /// Its <c>auth</c> secret.
    /// </summary>
    public byte[] AuthSecret { get; }

    /// <summary>
    /// <see cref="PublicKey"/> in base64url, as the browser gives it.
    /// </summary>
    public string PublicKeyText => Base64Url.EncodeToString(PublicKey);

    /// <summary>
    /// <see cref="AuthSecret"/> in base64url, as the browser gives it.
    /// </summary>
    public string AuthSecretText => Base64Url.EncodeToString(AuthSecret);

    /// <summary>
    /// The browser from RFC 8291's example, section 5.
    /// </summary>
    public static WebPushTestBrowser RfcExample()
    {
        byte[] publicKey = Base64Url.DecodeFromChars(
            "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4");
        ECDiffieHellman key = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..] },
            D = Base64Url.DecodeFromChars("q1dXpw3UpT5VOmu_cf_v6ih07Aems3njxI-JWgLcM94")
        });
        return new WebPushTestBrowser(key, Base64Url.DecodeFromChars("BTBZMqHH6r4Tts7J_aSIgg"));
    }

    /// <summary>
    /// Reads a push's body, one <c>aes128gcm</c> record.
    /// </summary>
    /// <param name="body">The body as the push service would hand it over.</param>
    /// <returns>The message.</returns>
    /// <exception cref="CryptographicException">It wasn't encrypted for this browser, or was changed.</exception>
    public byte[] Decrypt(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);

        byte[] salt = body[..16];
        uint recordSize = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4));
        int keyIdLength = body[20];
        byte[] serverPublicKey = body[21..(21 + keyIdLength)];
        byte[] record = body[(21 + keyIdLength)..];
        if (record.Length > recordSize)
        {
            throw new CryptographicException("More than one record.");
        }

        using ECDiffieHellman server = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = serverPublicKey[1..33], Y = serverPublicKey[33..] }
        });
        byte[] sharedSecret = _key.DeriveRawSecretAgreement(server.PublicKey);
        byte[] keyInfo = [.. Encoding.ASCII.GetBytes("WebPush: info\0"), .. PublicKey, .. serverPublicKey];
        byte[] inputKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, AuthSecret, keyInfo);
        byte[] contentKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, inputKey, 16, salt,
            Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        byte[] nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, inputKey, 12, salt,
            Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        byte[] padded = new byte[record.Length - 16];
        using AesGcm aes = new(contentKey, 16);
        aes.Decrypt(nonce, record[..^16], record[^16..], padded);

        // The message, then the last record's delimiter, then any zero padding
        int end = Array.FindLastIndex(padded, static x => x != 0);
        if (end < 0 || padded[end] != 0x02)
        {
            throw new CryptographicException("No last record delimiter.");
        }

        return padded[..end];
    }

    /// <inheritdoc/>
    public void Dispose() => _key.Dispose();
}
