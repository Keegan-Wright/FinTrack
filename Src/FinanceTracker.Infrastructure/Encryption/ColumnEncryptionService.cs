using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Infrastructure.Encryption;

/// <summary>
/// AES-256-GCM column encryption over a keyring. Stored values are <c>{keyId}:{base64(nonce | ciphertext | tag)}</c>:
/// a random nonce per value means equal values don't look equal when stored at rest. Tag means tampered values fail to
/// decrypt.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Singleton<IColumnEncryptionService>]
public class ColumnEncryptionService : IColumnEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const char KeyIdSeparator = ':';

    // Separate keys per purpose, derived from each keyring key
    private static readonly byte[] EncryptionKeyInfo = "FinTrack column encryption v1"u8.ToArray();
    private static readonly byte[] BlindIndexKeyInfo = "FinTrack blind index v1"u8.ToArray();

    private readonly Dictionary<int, byte[]> _encryptionKeys;
    private readonly byte[] _blindIndexKey;

    public ColumnEncryptionService(EncryptionConfiguration encryptionConfiguration)
    {
        ArgumentNullException.ThrowIfNull(encryptionConfiguration);

        if (!encryptionConfiguration.Keys.ContainsKey(encryptionConfiguration.CurrentKeyId))
        {
            throw new ArgumentException(
                $"The current encryption key {encryptionConfiguration.CurrentKeyId} isn't in the keyring.",
                nameof(encryptionConfiguration));
        }

        foreach ((int id, byte[] key) in encryptionConfiguration.Keys)
        {
            if (key.Length != EncryptionConfiguration.KeySize)
            {
                throw new ArgumentException(
                    $"Encryption key {id} must be {EncryptionConfiguration.KeySize} bytes.",
                    nameof(encryptionConfiguration));
            }
        }

        CurrentKeyId = encryptionConfiguration.CurrentKeyId;
        _encryptionKeys = encryptionConfiguration.Keys.ToDictionary(
            static x => x.Key,
            static x => DeriveKey(x.Value, EncryptionKeyInfo));
        _blindIndexKey = DeriveKey(encryptionConfiguration.Keys[CurrentKeyId], BlindIndexKeyInfo);
        KeyringFingerprint = ComputeFingerprint(encryptionConfiguration);
    }

    public int CurrentKeyId { get; }

    public string KeyringFingerprint { get; }

    public string Encrypt<T>(T value)
    {
        string? plainText = ToPlainText(value);
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] payload = new byte[NonceSize + plainBytes.Length + TagSize];
        Span<byte> nonce = payload.AsSpan(0, NonceSize);
        Span<byte> cipherBytes = payload.AsSpan(NonceSize, plainBytes.Length);
        Span<byte> tag = payload.AsSpan(NonceSize + plainBytes.Length, TagSize);

        RandomNumberGenerator.Fill(nonce);
        using (AesGcm aes = new(_encryptionKeys[CurrentKeyId], TagSize))
        {
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        return string.Create(CultureInfo.InvariantCulture, $"{CurrentKeyId}{KeyIdSeparator}{Convert.ToBase64String(payload)}");
    }

    public T? Decrypt<T>(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            // An empty collection encrypts as [], so an empty value is a column a migration added with an empty
            // default. Those rows have none rather than null, which the domain's collections never are
            return IsJsonCollection(typeof(T)) ? FromPlainText<T>("[]") : default;
        }

        int separator = cipherText.IndexOf(KeyIdSeparator, StringComparison.Ordinal);
        if (separator <= 0 ||
            !int.TryParse(cipherText.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out int keyId))
        {
            throw new CryptographicException("The value isn't in the encrypted format.");
        }

        if (!_encryptionKeys.TryGetValue(keyId, out byte[]? key))
        {
            throw new CryptographicException($"The value was encrypted with key {keyId}, which isn't in the keyring.");
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(cipherText[(separator + 1)..]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("The value isn't in the encrypted format.", ex);
        }

        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("The value isn't in the encrypted format.");
        }

        int cipherLength = payload.Length - NonceSize - TagSize;
        byte[] plainBytes = new byte[cipherLength];
        using (AesGcm aes = new(key, TagSize))
        {
            aes.Decrypt(
                payload.AsSpan(0, NonceSize),
                payload.AsSpan(NonceSize, cipherLength),
                payload.AsSpan(NonceSize + cipherLength, TagSize),
                plainBytes);
        }

        return FromPlainText<T>(Encoding.UTF8.GetString(plainBytes));
    }

    public string ComputeBlindIndex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToBase64String(HMACSHA256.HashData(_blindIndexKey, Encoding.UTF8.GetBytes(value)));
    }

    private static byte[] DeriveKey(byte[] keyringKey, byte[] info) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, keyringKey, EncryptionConfiguration.KeySize, salt: [], info);

    private static string ComputeFingerprint(EncryptionConfiguration configuration)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(BitConverter.GetBytes(configuration.CurrentKeyId));
        foreach ((int id, byte[] key) in configuration.Keys.OrderBy(static x => x.Key))
        {
            hash.AppendData(BitConverter.GetBytes(id));
            hash.AppendData(key);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>
    /// Invariant culture and round-trip formats so stored values don't depend on the host's locale.
    /// </summary>
    private static string? ToPlainText<T>(T value) => value switch
    {
        null => null,
        string text => text,
        byte[] byteArray => Convert.ToBase64String(byteArray),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        System.Collections.IEnumerable enumerable => System.Text.Json.JsonSerializer.Serialize(enumerable),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private static bool IsJsonCollection(Type type) =>
        type != typeof(string) && type != typeof(byte[]) &&
        typeof(System.Collections.IEnumerable).IsAssignableFrom(type);

    private static T FromPlainText<T>(string plainText)
    {
        if (typeof(T) == typeof(byte[]))
        {
            return (T)(object)Convert.FromBase64String(plainText);
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)plainText;
        }

        Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        return (T)ParsePlainText(plainText, targetType);
    }

    private static object ParsePlainText(string plainText, Type targetType)
    {
        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, plainText);
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(plainText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(plainText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (targetType.IsArray)
        {
            return System.Text.Json.JsonSerializer.Deserialize(plainText, targetType)
                   ?? Array.CreateInstanceFromArrayType(targetType, 0);
        }

        if (targetType.IsGenericType || typeof(System.Collections.IEnumerable).IsAssignableFrom(targetType))
        {
            return System.Text.Json.JsonSerializer.Deserialize(plainText, targetType)
                   ?? throw new InvalidOperationException($"Failed to deserialize collection of type {targetType}.");
        }

        return Convert.ChangeType(plainText, targetType, CultureInfo.InvariantCulture);
    }
}
