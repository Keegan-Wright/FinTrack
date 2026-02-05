using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Configurations;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Services.Encryption;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<ISymmetricEncryptionService>]
public class SymmetricEncryptionService : ISymmetricEncryptionService
{
 
    private const int KeySize = 256;
    private const int BlockSize = 128;
    
    private readonly Aes _aes;

    public SymmetricEncryptionService(EncryptionSettings encryptionSettings)
    {
        _aes = CreateAes(encryptionSettings, HashAlgorithmName.SHA3_256);
    }

    private Aes CreateAes(EncryptionSettings encryptionSettings, HashAlgorithmName hashAlgorithm)
    {
        var saltBytes = Encoding.ASCII.GetBytes(encryptionSettings.SymmetricSalt);
        var generator = new Span<byte>();
        Rfc2898DeriveBytes.Pbkdf2(encryptionSettings.SymmetricKey, saltBytes, generator, encryptionSettings.Iterations, hashAlgorithm);

        var aes = Aes.Create();
        aes.Key = generator.Slice(0, KeySize / 8).ToArray();
        aes.IV = generator.Slice(0, BlockSize / 8).ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        
        return aes;
    }

    public async Task<string> Encrypt(string plainText)
    {
        if(string.IsNullOrEmpty(plainText))
            return plainText;
        
        var bytes = Encoding.Unicode.GetBytes(plainText);
        
        using var encryptor = _aes.CreateEncryptor();
        using var memoryStream = new MemoryStream();
        
        using(var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
        {
            await cryptoStream.WriteAsync(bytes, 0, bytes.Length);
        }
        
        var encryptedBytes = memoryStream.ToArray();
        return Convert.ToBase64String(encryptedBytes);
        
    }

    public async Task<string> Decrypt(string cipherText)
    {
        if(string.IsNullOrEmpty(cipherText))
            return cipherText;

        var bytes = Convert.FromBase64String(cipherText);
        
        using var decryptor = _aes.CreateDecryptor();
        using var memoryStram = new MemoryStream();

        using (var cryptoStream = new CryptoStream(memoryStram, decryptor, CryptoStreamMode.Write))
        {
            await cryptoStream.WriteAsync(bytes, 0, bytes.Length);
        }
        
        var decryptedBytes = memoryStram.ToArray();
        return Encoding.Unicode.GetString(decryptedBytes);
    }
}