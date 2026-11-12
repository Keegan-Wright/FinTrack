namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Something a user sees, their own or another person's shared with them, see <see cref="SharedChosenItems"/>.
/// </summary>
/// <param name="Item">The thing, its owner's.</param>
/// <param name="SharedBy">Its owner's first and last name when it's someone else's, null when it's the user's.</param>
/// <param name="CanChange">Whether the user can change it.</param>
internal sealed record VisibleItem<T>(T Item, string? SharedBy, bool CanChange)
{
    public bool IsOwn => SharedBy is null;
}
