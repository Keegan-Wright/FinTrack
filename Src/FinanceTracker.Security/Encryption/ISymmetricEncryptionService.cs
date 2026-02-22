namespace FinanceTracker.Security.Encryption;

public interface ISymmetricEncryptionService
{
    string Encrypt<T>(T value);
    T Decrypt<T>(string cipherText);
}
