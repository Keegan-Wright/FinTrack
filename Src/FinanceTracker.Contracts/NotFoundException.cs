namespace FinanceTracker.Contracts;

/// <summary>
/// The user has no such thing to see or change, e.g. a budget id that's gone or someone else's that isn't shared with
/// them. The API answers 404.
/// </summary>
public sealed class NotFoundException : InvalidOperationException
{
    /// <summary>
    /// Not found, with no more said.
    /// </summary>
    public NotFoundException()
    {
    }

    /// <param name="message">What wasn't found, e.g. <c>No budget {id} the user can see was found.</c></param>
    public NotFoundException(string message) : base(message)
    {
    }

    /// <param name="message">What wasn't found.</param>
    /// <param name="innerException">What failed in looking.</param>
    public NotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
