using FinanceTracker.Infrastructure.Common.Encryption;

namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// A database context whose columns are encrypted and whose rows carry integrity tags. The
/// interceptors, query compiler and key rotator work through this, so any context can use them.
/// </summary>
public interface IEncryptedContext
{
    /// <summary>
    /// Encrypts and decrypts its columns, and makes their blind indexes and row tags.
    /// </summary>
    IColumnEncryptionService EncryptionService { get; }

    /// <summary>
    /// Rows loaded with JSON columns, checked once the query has filled them in.
    /// </summary>
    PendingRowIntegrityChecks PendingRowIntegrityChecks { get; }

    /// <summary>
    /// Whether rows tagged in an older format are let through. Only the key rotator sets it, while it tags them again.
    /// </summary>
    bool AcceptsOutdatedRowTags { get; set; }

    /// <summary>
    /// The clock, for blind index date ranges that run up to now.
    /// </summary>
    TimeProvider TimeProvider { get; }
}
