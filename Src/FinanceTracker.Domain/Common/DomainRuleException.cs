namespace FinanceTracker.Domain.Common;

/// <summary>
/// A change the domain's rules refuse, e.g. someone other than its owner changing what a shared goal tracks, or
/// answering an invite meant for someone else. Its message says which rule, for the person making the change. An
/// <see cref="InvalidOperationException"/>, as refusing the change leaves things as they were.
/// </summary>
public sealed class DomainRuleException : InvalidOperationException
{
    /// <summary>
    /// Makes one with the default message.
    /// </summary>
    public DomainRuleException()
    {
    }

    /// <summary>
    /// Makes one saying which rule refused the change.
    /// </summary>
    /// <param name="message">Which rule, for the person making the change.</param>
    public DomainRuleException(string message) : base(message)
    {
    }

    /// <summary>
    /// Makes one saying which rule refused the change, caused by <paramref name="innerException"/>.
    /// </summary>
    /// <param name="message">Which rule, for the person making the change.</param>
    /// <param name="innerException">What caused it.</param>
    public DomainRuleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
