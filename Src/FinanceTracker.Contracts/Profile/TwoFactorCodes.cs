namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// Codes from an authenticator app as people type them.
/// </summary>
public static class TwoFactorCodes
{
    /// <summary>
    /// How many digits an authenticator code has.
    /// </summary>
    public const int Length = 6;

    /// <summary>
    /// <paramref name="code"/> without the spaces and dashes people type between the digits.
    /// </summary>
    /// <param name="code">The code as typed.</param>
    public static string CleanInput(string? code) =>
        (code ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal)
        .Replace("-", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Whether <paramref name="code"/> is <see cref="Length"/> digits without its spaces and dashes.
    /// </summary>
    /// <param name="code">The code as typed.</param>
    public static bool IsCode(string? code)
    {
        string cleaned = CleanInput(code);
        return cleaned.Length == Length && cleaned.All(char.IsAsciiDigit);
    }
}
