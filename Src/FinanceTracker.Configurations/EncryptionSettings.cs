namespace FinanceTracker.Configurations;

public sealed class EncryptionSettings
{
    public string SymmetricKey { get; set; }
    public string SymmetricSalt { get; set; }
    public int Iterations { get; set; } = 600000;
}
