using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Infrastructure.Encryption;

[InjectionCategory(InjectionCategoryType.Service)]
[Singleton<ISymmetricEncryptionService>]
public class SymmetricEncryptionService : ISymmetricEncryptionService
{
    private const int KeySize = 256;
    private const int BlockSize = 128;

    private readonly byte[] _iv;
    private readonly byte[] _key;

    public SymmetricEncryptionService(EncryptionConfiguration encryptionConfiguration) =>
        (_key, _iv) = DeriveKeyAndIv(encryptionConfiguration, HashAlgorithmName.SHA3_256);

    public string Encrypt<T>(T value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        // Invariant culture and round-trip formats so stored values don't depend on the host's locale
        string? plainText = value switch
        {
            byte[] byteArray => Convert.ToBase64String(byteArray),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            System.Collections.IEnumerable enumerable when value is not string => System.Text.Json.JsonSerializer.Serialize(enumerable),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        byte[] bytes = Encoding.Unicode.GetBytes(plainText);

        using Aes aes = CreateAes();
        using ICryptoTransform encryptor = aes.CreateEncryptor();
        using MemoryStream memoryStream = new();

        using (CryptoStream cryptoStream = new(memoryStream, encryptor, CryptoStreamMode.Write))
        {
            cryptoStream.Write(bytes, 0, bytes.Length);
        }

        byte[] encryptedBytes = memoryStream.ToArray();
        return Convert.ToBase64String(encryptedBytes);
    }

    public T? Decrypt<T>(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return default;
        }

        byte[] bytes = Convert.FromBase64String(cipherText);

        using Aes aes = CreateAes();
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        using MemoryStream memoryStream = new();


        using (CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Write))
        {
            cryptoStream.Write(bytes, 0, bytes.Length);
        }

        byte[] decryptedBytes = memoryStream.ToArray();
        string plainText = Encoding.Unicode.GetString(decryptedBytes);

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

    private static (byte[] Key, byte[] Iv) DeriveKeyAndIv(EncryptionConfiguration encryptionConfiguration,
        HashAlgorithmName hashAlgorithm)
    {
        int keyLen = KeySize / 8;
        int ivLen = BlockSize / 8;

        byte[] saltBytes = Encoding.ASCII.GetBytes(encryptionConfiguration.SymmetricSalt);
        Span<byte> generator = new byte[256];
        Rfc2898DeriveBytes.Pbkdf2(encryptionConfiguration.SymmetricKey, saltBytes, generator, encryptionConfiguration.Iterations,
            hashAlgorithm);

        return (generator[..(keyLen)].ToArray(), generator[keyLen..(keyLen + ivLen)].ToArray());
    }

    private Aes CreateAes()
    {
        Aes aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        return aes;
    }
}
