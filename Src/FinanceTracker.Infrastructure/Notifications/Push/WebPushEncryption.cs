using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// Encrypts a push message for one browser, RFC 8291's <c>aes128gcm</c> content encoding. Only that browser can read
/// it, the push service in between sees the size and nothing else. The cipher and its 128-bit key are what the RFC
/// sets, browsers accept nothing else. Each message gets its own key from a fresh ECDH key pair and salt.
/// </summary>
public static class WebPushEncryption
{
    /// <summary>
    /// The one record's size in the header, the RFC's 4096 bytes.
    /// </summary>
    public const int RecordSize = 4096;

    /// <summary>
    /// The most a message can hold, what fits one record with its key, header, padding delimiter and tag.
    /// </summary>
    public const int MostPlaintextBytes = RecordSize - HeaderLength - 1 - TagLength;

    /// <summary>
    /// The salt's length.
    /// </summary>
    private const int SaltLength = 16;

    /// <summary>
    /// An uncompressed P-256 public key's length, 0x04 then X and Y.
    /// </summary>
    private const int PublicKeyLength = 65;

    /// <summary>
    /// The auth secret the browser gives with its key.
    /// </summary>
    private const int AuthSecretLength = 16;

    /// <summary>
    /// The salt, record size, key id length and key id, the server's public key.
    /// </summary>
    private const int HeaderLength = SaltLength + 4 + 1 + PublicKeyLength;

    /// <summary>
    /// AES-GCM's tag.
    /// </summary>
    private const int TagLength = 16;

    /// <summary>
    /// Marks the last record, put after the message.
    /// </summary>
    private const byte LastRecordDelimiter = 0x02;

    private static readonly byte[] KeyInfoPrefix = Encoding.ASCII.GetBytes("WebPush: info\0");
    private static readonly byte[] ContentKeyInfo = Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0");
    private static readonly byte[] NonceInfo = Encoding.ASCII.GetBytes("Content-Encoding: nonce\0");

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> for the browser with <paramref name="browserPublicKey"/> and
    /// <paramref name="authSecret"/>, the keys it gave when it subscribed.
    /// </summary>
    /// <param name="plaintext">The message.</param>
    /// <param name="browserPublicKey">The browser's <c>p256dh</c> key, uncompressed.</param>
    /// <param name="authSecret">The browser's <c>auth</c> secret.</param>
    /// <returns>The request body, header then the encrypted record.</returns>
    /// <exception cref="ArgumentException">A key is the wrong length, or the message is longer than
    /// <see cref="MostPlaintextBytes"/>.</exception>
    /// <exception cref="CryptographicException">The browser's key isn't a P-256 point.</exception>
    public static byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> browserPublicKey,
        ReadOnlySpan<byte> authSecret)
    {
        using ECDiffieHellman serverKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        return Encrypt(plaintext, browserPublicKey, authSecret, serverKey, salt);
    }

    /// <summary>
    /// <see cref="Encrypt(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> with the server's key pair
    /// and salt given, for checking against the RFC's example. Never reuse either for real messages.
    /// </summary>
    /// <param name="plaintext">The message.</param>
    /// <param name="browserPublicKey">The browser's <c>p256dh</c> key, uncompressed.</param>
    /// <param name="authSecret">The browser's <c>auth</c> secret.</param>
    /// <param name="serverKey">The server's one-off key pair.</param>
    /// <param name="salt">The one-off salt.</param>
    internal static byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> browserPublicKey,
        ReadOnlySpan<byte> authSecret, ECDiffieHellman serverKey, ReadOnlySpan<byte> salt)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(plaintext.Length, MostPlaintextBytes, nameof(plaintext));
        if (browserPublicKey.Length != PublicKeyLength || browserPublicKey[0] != 0x04)
        {
            throw new ArgumentException("The browser's key should be an uncompressed P-256 point.",
                nameof(browserPublicKey));
        }

        if (authSecret.Length != AuthSecretLength)
        {
            throw new ArgumentException($"The auth secret should be {AuthSecretLength} bytes.", nameof(authSecret));
        }

        byte[] serverPublicKey = UncompressedPublicKey(serverKey);

        // The shared secret mixed with the browser's auth secret, RFC 8291 section 3.3
        using ECDiffieHellman browserKey = ECDiffieHellman.Create(PublicKeyParameters(browserPublicKey));
        byte[] sharedSecret = serverKey.DeriveRawSecretAgreement(browserKey.PublicKey);
        byte[] keyInfo = [.. KeyInfoPrefix, .. browserPublicKey, .. serverPublicKey];
        byte[] inputKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret.ToArray(), keyInfo);

        // The content key and nonce, RFC 8188 section 2.2 and 2.3
        byte[] pseudorandomKey = HKDF.Extract(HashAlgorithmName.SHA256, inputKey, salt.ToArray());
        byte[] contentKey = HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, 16, ContentKeyInfo);
        byte[] nonce = HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, 12, NonceInfo);

        byte[] padded = [.. plaintext, LastRecordDelimiter];
        byte[] body = new byte[HeaderLength + padded.Length + TagLength];
        WriteHeader(body, salt, serverPublicKey);

        using AesGcm aes = new(contentKey, TagLength);
        aes.Encrypt(nonce, padded, body.AsSpan(HeaderLength, padded.Length), body.AsSpan(HeaderLength + padded.Length));

        CryptographicOperations.ZeroMemory(sharedSecret);
        CryptographicOperations.ZeroMemory(inputKey);
        CryptographicOperations.ZeroMemory(pseudorandomKey);
        CryptographicOperations.ZeroMemory(contentKey);
        return body;
    }

    /// <summary>
    /// The salt, the record size, and the server's public key as the key id, RFC 8188 section 2.1.
    /// </summary>
    /// <param name="body">The body to write into.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="serverPublicKey">The server's one-off public key.</param>
    private static void WriteHeader(Span<byte> body, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> serverPublicKey)
    {
        salt.CopyTo(body);
        BinaryPrimitives.WriteUInt32BigEndian(body[SaltLength..], RecordSize);
        body[SaltLength + 4] = PublicKeyLength;
        serverPublicKey.CopyTo(body[(SaltLength + 5)..]);
    }

    /// <summary>
    /// A key pair's public key, uncompressed.
    /// </summary>
    /// <param name="key">The key pair.</param>
    private static byte[] UncompressedPublicKey(ECDiffieHellman key)
    {
        ECParameters parameters = key.ExportParameters(includePrivateParameters: false);
        return [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
    }

    /// <summary>
    /// The parameters of an uncompressed P-256 public key.
    /// </summary>
    /// <param name="publicKey">The key, 0x04 then X and Y.</param>
    private static ECParameters PublicKeyParameters(ReadOnlySpan<byte> publicKey) => new()
    {
        Curve = ECCurve.NamedCurves.nistP256,
        Q = new ECPoint { X = publicKey.Slice(1, 32).ToArray(), Y = publicKey.Slice(33, 32).ToArray() }
    };
}
