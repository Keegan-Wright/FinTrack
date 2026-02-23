using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Configurations;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Security.Encryption;

[InjectionCategory(InjectionCategoryType.Service)]
[Singleton<ISymmetricEncryptionService>]
public class SymmetricEncryptionService : ISymmetricEncryptionService
{
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private readonly byte[] _iv;

    private readonly byte[] _key;

    public SymmetricEncryptionService(EncryptionSettings encryptionSettings) =>
        (_key, _iv) = DeriveKeyAndIv(encryptionSettings, HashAlgorithmName.SHA3_256);

    public string Encrypt<T>(T value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        string? plainText;
        if (value is byte[] byteArray)
        {
            plainText = Convert.ToBase64String(byteArray);
        }
        else
        {
            plainText = value.ToString();
        }

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
        using MemoryStream memoryStram = new();

        using (CryptoStream cryptoStream = new(memoryStram, decryptor, CryptoStreamMode.Write))
        {
            cryptoStream.Write(bytes, 0, bytes.Length);
        }

        byte[] decryptedBytes = memoryStram.ToArray();
        string plainText = Encoding.Unicode.GetString(decryptedBytes);

        if (typeof(T) == typeof(byte[]))
        {
            return (T)(object)Convert.FromBase64String(plainText);
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)plainText;
        }

        Type targetType = typeof(T);
        if (!targetType.IsGenericType || targetType.GetGenericTypeDefinition() != typeof(Nullable<>))
        {
            return (T)Convert.ChangeType(plainText, targetType, CultureInfo.CurrentCulture);
        }

        if (string.IsNullOrEmpty(plainText))
        {
            return default;
        }

        targetType = Nullable.GetUnderlyingType(targetType)!;

        return (T)Convert.ChangeType(plainText, targetType, CultureInfo.CurrentCulture);
    }

    private static (byte[] Key, byte[] Iv) DeriveKeyAndIv(EncryptionSettings encryptionSettings,
        HashAlgorithmName hashAlgorithm)
    {
        byte[] saltBytes = Encoding.ASCII.GetBytes(encryptionSettings.SymmetricSalt);
        Span<byte> generator = new byte[256].AsSpan();
        Rfc2898DeriveBytes.Pbkdf2(encryptionSettings.SymmetricKey, saltBytes, generator, encryptionSettings.Iterations,
            hashAlgorithm);

        return (generator[..(KeySize / 8)].ToArray(), generator[..(BlockSize / 8)].ToArray());
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
