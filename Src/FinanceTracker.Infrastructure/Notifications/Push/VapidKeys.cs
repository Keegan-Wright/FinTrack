using System.Security.Cryptography;

namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// The server's VAPID key pair, P-256, which signs every push so push services know it's from this FinTrack. Browsers
/// are given the public key when they subscribe, and only take pushes signed with it.
/// </summary>
public sealed class VapidKeys
{
    /// <summary>
    /// A P-256 private key's length.
    /// </summary>
    private const int PrivateKeyLength = 32;

    /// <param name="publicKey">The public key, uncompressed, 0x04 then X and Y.</param>
    /// <param name="privateKey">The private key, D.</param>
    /// <exception cref="ArgumentException">A key is the wrong length.</exception>
    public VapidKeys(byte[] publicKey, byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(privateKey);
        if (publicKey.Length != 65 || publicKey[0] != 0x04)
        {
            throw new ArgumentException("The public key should be an uncompressed P-256 point.", nameof(publicKey));
        }

        if (privateKey.Length != PrivateKeyLength)
        {
            throw new ArgumentException($"The private key should be {PrivateKeyLength} bytes.", nameof(privateKey));
        }

        PublicKey = publicKey;
        PrivateKey = privateKey;
    }

    /// <summary>
    /// The public key, uncompressed.
    /// </summary>
    public byte[] PublicKey { get; }

    /// <summary>
    /// The private key, D.
    /// </summary>
    public byte[] PrivateKey { get; }

    /// <summary>
    /// The public key in base64url, as browsers take it to subscribe and push services in the header.
    /// </summary>
    public string PublicKeyText => System.Buffers.Text.Base64Url.EncodeToString(PublicKey);

    /// <summary>
    /// A new key pair.
    /// </summary>
    public static VapidKeys Create()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = key.ExportParameters(includePrivateParameters: true);
        return new VapidKeys([0x04, .. parameters.Q.X!, .. parameters.Q.Y!], parameters.D!);
    }

    /// <summary>
    /// The key pair to sign with. Dispose it once done.
    /// </summary>
    public ECDsa ToSigningKey() => ECDsa.Create(new ECParameters
    {
        Curve = ECCurve.NamedCurves.nistP256,
        Q = new ECPoint { X = PublicKey[1..33], Y = PublicKey[33..] },
        D = PrivateKey
    });
}
