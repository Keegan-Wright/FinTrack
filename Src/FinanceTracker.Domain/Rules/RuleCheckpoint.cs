using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Rules;

/// <summary>
/// When a user's rules were last applied, so the next run only looks at transactions that have arrived or changed
/// since.
/// </summary>
public sealed class RuleCheckpoint : AggregateRoot
{
    [Obsolete("EF Use Only")]
    private RuleCheckpoint()
    {
    }

    /// <param name="userId">The user it's for.</param>
    /// <param name="appliedFrom">When the first run started loading.</param>
    public RuleCheckpoint(Guid userId, DateTime appliedFrom) : base(userId) => AppliedFrom = appliedFrom;

    /// <summary>
    /// When the last run started loading. Anything created or updated after this hasn't had the rules applied yet.
    /// </summary>
    public DateTime AppliedFrom { get; private set; }

    /// <summary>
    /// Records a run, so the next only looks at what's changed since.
    /// </summary>
    /// <param name="appliedFrom">When the run started loading.</param>
    public void Record(DateTime appliedFrom) => AppliedFrom = appliedFrom;
}
