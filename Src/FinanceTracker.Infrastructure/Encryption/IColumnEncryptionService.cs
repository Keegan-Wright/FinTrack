namespace FinanceTracker.Infrastructure.Encryption;

public interface IColumnEncryptionService
{
    /// <summary>
    /// The key new values are encrypted with, stored values start with <c>{CurrentKeyId}:</c>.
    /// </summary>
    int CurrentKeyId { get; }

    /// <summary>
    /// Identifies the keyring without revealing it. Services with the same keys share an EF model.
    /// </summary>
    string KeyringFingerprint { get; }

    /// <summary>
    /// Encrypts <paramref name="value"/> with the current key. Encrypting the same value twice gives different
    /// results, use <see cref="ComputeBlindIndex"/> for anything that needs matching.
    /// </summary>
    string Encrypt<T>(T value);

    /// <summary>
    /// Decrypts a value written by <see cref="Encrypt{T}"/> under any key in the keyring.
    /// </summary>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The value was tampered with, or its key isn't in the keyring.
    /// </exception>
    T? Decrypt<T>(string cipherText);

    /// <summary>
    /// A keyed hash of <paramref name="value"/> under the current key. Equal values give equal hashes, so it can be
    /// indexed and queried while revealing nothing without the key.
    /// </summary>
    string ComputeBlindIndex(string value);
}
