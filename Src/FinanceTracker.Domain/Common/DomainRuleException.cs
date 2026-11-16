namespace FinanceTracker.Domain.Common;

/// <summary>
/// A change the domain's rules refuse, e.g. someone other than its owner changing what a shared goal tracks, or
/// answering an invite meant for someone else. Its message says which rule, for the person making the change. An
/// <see cref="InvalidOperationException"/>, as refusing the change leaves things as they were.
/// </summary>
public sealed class DomainRuleException : InvalidOperationException
{
    public DomainRuleException()
    {
    }

    public DomainRuleException(string message) : base(message)
    {
    }

    public DomainRuleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
