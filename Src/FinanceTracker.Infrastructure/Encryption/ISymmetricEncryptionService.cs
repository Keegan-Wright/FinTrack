namespace FinanceTracker.Infrastructure.Encryption;

public interface ISymmetricEncryptionService
{
    string Encrypt<T>(T value);
    T? Decrypt<T>(string cipherText);
}
