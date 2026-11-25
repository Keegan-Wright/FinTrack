namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// A row failed its integrity check when loaded. It was changed in the database outside the app, e.g. moved to
/// another user, see <see cref="RowIntegrity"/>.
/// </summary>
public sealed class RowIntegrityException : Exception
{
    /// <param name="table">The row's table.</param>
    /// <param name="id">The row's id.</param>
    public RowIntegrityException(string table, string id)
        : base($"Row {id} in {table} failed its integrity check, it was changed outside FinTrack")
    {
        Table = table;
        Id = id;
    }

    /// <summary>
    /// Says a row failed its integrity check.
    /// </summary>
    public RowIntegrityException() : base("A row failed its integrity check")
    {
    }

    /// <param name="message">What went wrong.</param>
    public RowIntegrityException(string message) : base(message)
    {
    }

    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">What caused it.</param>
    public RowIntegrityException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// The row's table, null when not known.
    /// </summary>
    public string? Table { get; }

    /// <summary>
    /// The row's id, null when not known.
    /// </summary>
    public string? Id { get; }
}
