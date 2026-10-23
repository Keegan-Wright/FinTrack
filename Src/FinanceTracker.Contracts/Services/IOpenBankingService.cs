using FinanceTracker.Contracts.OpenBanking;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Connecting the signed in user's banks through the open banking provider (TrueLayer).
/// </summary>
/// <remarks>
/// Connecting is a round trip through the provider: pick banks from
/// <see cref="GetOpenBankingProvidersForClientAsync"/>, send the user to <see cref="BuildAuthUrlAsync"/>, then hand the
/// access code they come back with to <see cref="AddVendorViaAccessCodeAsync"/>.
/// </remarks>
public interface IOpenBankingService
{
    /// <summary>
    /// The banks that can be connected, by display name.
    /// </summary>
    IAsyncEnumerable<OpenBankingProvider> GetOpenBankingProvidersForClientAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// The provider's consent page for the chosen banks and scopes.
    /// </summary>
    Task<string> BuildAuthUrlAsync(ProviderSetupUrl setupProvider, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges the access code from the provider's consent page for tokens, connects (or reconnects) each bank it
    /// covers, then syncs them.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> once the banks are connected and synced. Never <see langword="false"/>, failures throw.
    /// </returns>
    /// <exception cref="HttpRequestException">The provider rejected the access code, or couldn't be reached.</exception>
    Task<bool> AddVendorViaAccessCodeAsync(AddVendor addVendor, CancellationToken cancellationToken);
}
