namespace FinanceTracker.Security.Encryption;

public interface ISymmetricEncryptionService
{
    Task<string> EncryptAsync(string plainText);
    Task<string> DecryptAsync(string cipherText);
}