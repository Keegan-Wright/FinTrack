using System.ComponentModel;

namespace FinanceTracker.Contracts.OpenBanking;

/// <summary>
/// The banks and data to ask the user's consent for.
/// </summary>
public class ProviderSetupUrl
{
    /// <summary>
    /// The banks, see <see cref="OpenBankingProvider.ProviderId"/>.
    /// </summary>
    [Description("Collection of banking provider identifiers to set up")]
    public required IReadOnlyList<string> ProviderIds { get; init; }

    /// <summary>
    /// The data to ask for, see <see cref="OpenBankingProvider.Scopes"/>.
    /// </summary>
    [Description("Collection of access scopes required for the banking integration")]
    public required IReadOnlyList<string> Scopes { get; init; }
}
