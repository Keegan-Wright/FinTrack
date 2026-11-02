namespace FinanceTracker.Contracts.Classifications;

/// <summary>
/// The user already has a custom classification called <see cref="Tag"/>, in any case.
/// </summary>
public sealed class DuplicateTagException : Exception
{
    /// <param name="tag">The name that's taken, as the user typed it.</param>
    public DuplicateTagException(string tag) : base($"You already have a tag called {tag}") => Tag = tag;

    /// <summary>
    /// The name that's taken.
    /// </summary>
    public string Tag { get; }
}
