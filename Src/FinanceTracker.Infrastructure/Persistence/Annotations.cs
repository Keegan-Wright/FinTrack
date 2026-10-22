namespace FinanceTracker.Infrastructure.Persistence;

public static class Annotations
{
    /// <summary>
    /// Marks a column as one to be encrpted at rest.
    /// </summary>
    internal const string EncryptedPropertyAnnotation = "FinanceTracker.EncrptedColumn";

    /// <summary>
    /// On a blind index shadow property, the name of the encrypted property it hashes.
    /// </summary>
    internal const string BlindIndexSourceAnnotation = "FinanceTracker.BlindIndexSource";
}
