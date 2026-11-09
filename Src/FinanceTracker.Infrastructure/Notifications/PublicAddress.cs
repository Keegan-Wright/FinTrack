namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// The app's address as people reach it, from <c>FINTRACK_PUBLIC_URL</c>, for links in emails and webhook posts.
/// </summary>
/// <param name="Url">Ending in <c>/</c> so links keep a sub path, null when it isn't known.</param>
public sealed record PublicAddress(Uri? Url)
{
    /// <summary>
    /// The address isn't known, so there are no links.
    /// </summary>
    public static PublicAddress Unknown { get; } = new((Uri?)null);

    /// <summary>
    /// <paramref name="path"/> in the app as a whole address, null when the app's address isn't known or there's no
    /// path.
    /// </summary>
    public Uri? LinkTo(string? path)
    {
        if (Url is null || path is null)
        {
            return null;
        }

        return new Uri(Url, path);
    }
}
