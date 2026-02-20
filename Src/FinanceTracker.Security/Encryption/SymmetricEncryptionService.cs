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
    
    private readonly EncryptionSettings _encryptionSettings;

    public SymmetricEncryptionService(EncryptionSettings encryptionSettings)
    {
        _encryptionSettings = encryptionSettings;
    }

    private Aes CreateAes(EncryptionSettings encryptionSettings, HashAlgorithmName hashAlgorithm)
    {
        var saltBytes = Encoding.ASCII.GetBytes(encryptionSettings.SymmetricSalt);
        var key = Encoding.ASCII.GetBytes(encryptionSettings.SymmetricKey);
        var generator = new byte[256].AsSpan();
        Rfc2898DeriveBytes.Pbkdf2(password: encryptionSettings.SymmetricKey, salt: saltBytes, destination: generator, encryptionSettings.Iterations, hashAlgorithm);

        var aes = Aes.Create();
        aes.Key = generator.Slice(0, KeySize / 8).ToArray();
        aes.IV = generator.Slice(0, BlockSize / 8).ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        
        return aes;
    }

    public string Encrypt<T>(T value)
    {
        if (value == null)
            return null;

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
            return plainText;

        var bytes = Encoding.Unicode.GetBytes(plainText);

        using var aes = CreateAes(_encryptionSettings, HashAlgorithmName.SHA3_256);
        using var encryptor = aes.CreateEncryptor();
        using var memoryStream = new MemoryStream();

        using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
        {
             cryptoStream.Write(bytes, 0, bytes.Length);
        }

        var encryptedBytes = memoryStream.ToArray();
        return Convert.ToBase64String(encryptedBytes);

    }

    public T Decrypt<T>(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return default;

        var bytes = Convert.FromBase64String(cipherText);

        using var aes = CreateAes(_encryptionSettings, HashAlgorithmName.SHA3_256);
        using var decryptor = aes.CreateDecryptor();
        using var memoryStram = new MemoryStream();

        using (var cryptoStream = new CryptoStream(memoryStram, decryptor, CryptoStreamMode.Write))
        {
            cryptoStream.Write(bytes, 0, bytes.Length);
        }

        var decryptedBytes = memoryStram.ToArray();
        var plainText = Encoding.Unicode.GetString(decryptedBytes);

        if (typeof(T) == typeof(byte[]))
        {
            return (T)(object)Convert.FromBase64String(plainText);
        }

        if (typeof(T) == typeof(string))
            return (T)(object)plainText;

        var targetType = typeof(T);
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (string.IsNullOrEmpty(plainText))
                return default;
            targetType = Nullable.GetUnderlyingType(targetType)!;
        }

        return (T)Convert.ChangeType(plainText, targetType);
    }
}