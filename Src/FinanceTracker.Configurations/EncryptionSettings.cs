namespace FinanceTracker.Configurations;

public sealed class EncryptionSettings
{
    public required string SymmetricKey { get; init; }
    public required string SymmetricSalt { get; init; }
    public int Iterations { get; init; } = 600000;
}
