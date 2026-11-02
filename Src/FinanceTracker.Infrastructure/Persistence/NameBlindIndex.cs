namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// A blind index on a name, ignoring case and spaces at either end, so a unique index keeps a user's names apart the
/// way <see cref="NameCheck"/> does. "Holiday" and " holiday" hash the same.
/// </summary>
/// <remarks>
/// A name in more than one column, like a person's first and last name, is hashed as its parts joined by a space.
/// </remarks>
public static class NameBlindIndex
{
    /// <summary>
    /// The shadow property holding it, the same on every table that has one.
    /// </summary>
    public const string PropertyName = "NameKeyBlindIndex";

    /// <summary>
    /// <paramref name="parts"/> as compared, trimmed, joined by a space and in upper case.
    /// </summary>
    public static string Normalise(params IEnumerable<string?> parts) =>
        string.Join(' ', parts.Select(static x => (x ?? string.Empty).Trim())).ToUpperInvariant();

    /// <summary>
    /// What's hashed for <paramref name="parts"/>. Prefixed so it never shares a hash with another index's value.
    /// </summary>
    public static string Value(params IEnumerable<string?> parts) => $"name:{Normalise(parts)}";
}
