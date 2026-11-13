namespace FinanceTracker.Contracts;

/// <summary>
/// The change was refused by one of the app's rules, e.g. someone other than its owner changing what a shared goal
/// tracks. What the browser's services throw when the API answers 422.
/// </summary>
public sealed class RuleBrokenException : InvalidOperationException
{
    /// <summary>
    /// Refused, with no more said.
    /// </summary>
    public RuleBrokenException()
    {
    }

    /// <param name="message">Which rule refused it.</param>
    public RuleBrokenException(string message) : base(message)
    {
    }

    /// <param name="message">Which rule refused it.</param>
    /// <param name="innerException">What the refusal came with.</param>
    public RuleBrokenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
