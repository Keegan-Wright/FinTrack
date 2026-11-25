namespace FinanceTracker.Ai.Persistence;

/// <summary>
/// One user's AI choices.
/// </summary>
public class AiUserSetting
{
    /// <summary>
    /// The user.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Whether they've turned AI on for themselves.
    /// </summary>
    public bool OptedIn { get; set; }
}
