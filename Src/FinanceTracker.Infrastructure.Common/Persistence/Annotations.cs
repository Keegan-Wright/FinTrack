namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// The EF model annotations column encryption and blind indexes use.
/// </summary>
public static class Annotations
{
    /// <summary>
    /// Marks a column as one to be encrypted at rest.
    /// </summary>
    internal const string EncryptedPropertyAnnotation = "FinanceTracker.EncryptedColumn";

    /// <summary>
    /// On a blind index shadow property, the name of the encrypted property it hashes.
    /// </summary>
    internal const string BlindIndexSourceAnnotation = "FinanceTracker.BlindIndexSource";

    /// <summary>
    /// On a day blind index shadow property, the name of the encrypted date property whose UTC day it hashes.
    /// </summary>
    internal const string DayBlindIndexSourceAnnotation = "FinanceTracker.DayBlindIndexSource";

    /// <summary>
    /// On a name blind index shadow property, the encrypted properties whose name it hashes, comma separated in
    /// order, see <see cref="NameBlindIndex"/>.
    /// </summary>
    internal const string NameBlindIndexSourceAnnotation = "FinanceTracker.NameBlindIndexSource";

    /// <summary>
    /// Marks an entity whose rows carry an integrity tag, see <see cref="RowIntegrity"/>.
    /// </summary>
    internal const string RowIntegrityAnnotation = "FinanceTracker.RowIntegrity";
}
