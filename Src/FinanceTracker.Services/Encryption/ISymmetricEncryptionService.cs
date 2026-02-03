namespace FinanceTracker.Services.Encryption;

public interface ISymmetricEncryptionService
{
    Task<string> Encrypt(string plainText);
    Task<string> Decrypt(string cipherText);
}