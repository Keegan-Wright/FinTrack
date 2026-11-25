using FinanceTracker.Contracts.Tokens;

namespace FinanceTracker.Client.Pages;

/// <summary>
/// What each token is for, shown only for tokens that aren't for reading the API, which only the AI build has.
/// </summary>
public partial class ApiTokensPage
{
    /// <summary>
    /// What <paramref name="token"/> is for when it's not reading the API, null when it is.
    /// </summary>
    /// <param name="token">The token.</param>
    private static string? UseOf(TokenSummary token)
    {
#if FINTRACK_AI
        return token.ForAiClients ? "For AI clients" : null;
#else
        ArgumentNullException.ThrowIfNull(token);
        return null;
#endif
    }
}
