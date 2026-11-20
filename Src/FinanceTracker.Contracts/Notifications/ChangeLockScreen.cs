namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// Whether pushes say amounts on a locked phone's screen.
/// </summary>
public sealed class ChangeLockScreen
{
    /// <summary>
    /// Whether to say them. Without, a push says what it's about, e.g. <c>Groceries is nearly used up</c>.
    /// </summary>
    public bool ShowAmounts { get; set; }
}
