using FinanceTracker.Contracts.Sharing;

namespace FinanceTracker.Client.Components.Sharing;

/// <summary>
/// One of the user's things in a <see cref="ShareChoiceList"/>.
/// </summary>
/// <param name="Id">Its id, as its own feature knows it.</param>
/// <param name="Name">What it's called.</param>
/// <param name="Group">The heading it's listed under, such as an account's bank, null for none.</param>
/// <param name="FollowsAccount">The shared account it goes with, so it can't be chosen apart from it, null when it can.
/// </param>
/// <param name="FollowsMode">How that account is shared.</param>
public sealed record ShareChoiceItem(
    Guid Id,
    string Name,
    string? Group = null,
    string? FollowsAccount = null,
    ShareMode FollowsMode = ShareMode.ReadOnly)
{
    public bool CanChoose => FollowsAccount is null;
}
