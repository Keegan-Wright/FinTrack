namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// A transaction can't be split as asked, e.g. it's pending or the parts don't add up to its amount. The message says
/// why, for the user.
/// </summary>
public sealed class CannotSplitTransactionException : Exception
{
    /// <param name="message">Why, for the user.</param>
    public CannotSplitTransactionException(string message) : base(message)
    {
    }

    /// <param name="message">Why, for the user.</param>
    /// <param name="innerException">What the transaction refused with.</param>
    public CannotSplitTransactionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
