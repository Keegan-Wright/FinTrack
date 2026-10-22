using System.Globalization;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// The keyring for column encryption. Every key stays readable, new values are written with
/// <see cref="CurrentKeyId"/>. Rotating is adding a key, pointing <see cref="CurrentKeyId"/> at it and restarting,
/// startup re-encrypts anything still under an older key.
/// </summary>
public sealed class EncryptionConfiguration
{
    /// <summary>
    /// Key length in bytes, AES-256.
    /// </summary>
    public const int KeySize = 32;

    public required IReadOnlyDictionary<int, byte[]> Keys { get; init; }

    public required int CurrentKeyId { get; init; }

    /// <summary>
    /// Reads keys written as <c>id:base64key</c> pairs separated by <c>;</c>, e.g. <c>1:AbC...=;2:XyZ...=</c>.
    /// </summary>
    /// <exception cref="FormatException">The keys aren't in that format.</exception>
    public static EncryptionConfiguration Parse(string keys, int currentKeyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keys);

        Dictionary<int, byte[]> keyring = [];
        foreach (string entry in keys.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = entry.Split(':', 2);
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id))
            {
                throw new FormatException("Encryption keys must be written as id:base64key pairs separated by ';'.");
            }

            if (!keyring.TryAdd(id, Convert.FromBase64String(parts[1])))
            {
                throw new FormatException($"Encryption key {id} is listed more than once.");
            }
        }

        return new EncryptionConfiguration { Keys = keyring, CurrentKeyId = currentKeyId };
    }

    /// <summary>
    /// A new random key, base64 encoded, for adding to the keyring.
    /// </summary>
    public static string GenerateKey() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(KeySize));
}
