using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// How a user sees what others share with them. One per user, a user without one sees everything shared with them.
/// </summary>
public sealed class SharingSettings : AggregateRoot
{
    [Obsolete("EF Use Only")]
    private SharingSettings()
    {
    }

    /// <summary>
    /// Settings that show everything shared with the user.
    /// </summary>
    /// <param name="userId">The user they're for.</param>
    public SharingSettings(Guid userId) : base(userId)
    {
        IncludeShared = true;
    }

    /// <summary>
    /// Whether accounts shared with them show beside their own, Everyone, or only their own do, Just mine.
    /// </summary>
    public bool IncludeShared { get; private set; }

    /// <summary>
    /// Sets whether accounts shared with them show beside their own.
    /// </summary>
    /// <param name="includeShared">Whether to show them.</param>
    public void ShowShared(bool includeShared) => IncludeShared = includeShared;
}
