namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// Where a link stands from the signed in user's side.
/// </summary>
public enum LinkState
{
    /// <summary>
    /// They invited the other person, who hasn't answered yet.
    /// </summary>
    Invited = 0,

    /// <summary>
    /// The other person invited them, waiting for their answer.
    /// </summary>
    InvitedYou = 1,

    /// <summary>
    /// Both agreed, they're linked.
    /// </summary>
    Linked = 2
}
