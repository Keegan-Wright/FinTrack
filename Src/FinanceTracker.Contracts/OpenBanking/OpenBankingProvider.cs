namespace FinanceTracker.Contracts.OpenBanking;

/// <summary>
/// A bank that can be connected through the open banking provider.
/// </summary>
public sealed record OpenBankingProvider
{
    /// <summary>
    /// The provider's id for the bank, passed back in <see cref="ProviderSetupUrl.ProviderIds"/>.
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// The bank's name as shown to users.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The bank's country, an ISO 3166-1 alpha-2 code such as <c>gb</c>.
    /// </summary>
    public required string Country { get; init; }

    /// <summary>
    /// Where the bank's logo is hosted.
    /// </summary>
    public required string LogoUrl { get; init; }

    /// <summary>
    /// The data scopes the bank supports (e.g. <c>accounts</c>, <c>transactions</c>), passed back in
    /// <see cref="ProviderSetupUrl.Scopes"/>.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];
}
