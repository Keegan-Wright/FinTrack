using FinanceTracker.Contracts.OpenBanking;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Connecting the signed in user's banks through the open banking provider (TrueLayer).
/// </summary>
/// <remarks>
/// Connecting is a round trip through the provider: pick banks from
/// <see cref="GetOpenBankingProvidersForClientAsync"/>, send the user to <see cref="BuildAuthUrlAsync"/>, then hand the
/// access code they come back with to <see cref="AddVendorViaAccessCodeAsync"/>. Renewing a connection's consent is the
/// same round trip, starting from <see cref="GetRenewalUrlAsync"/> instead.
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
    /// covers, then queues a sync of the user's connections, which runs in the background.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> once the banks are connected and the sync queued. Never <see langword="false"/>, failures
    /// throw.
    /// </returns>
    /// <exception cref="HttpRequestException">The provider rejected the access code, or couldn't be reached.</exception>
    Task<bool> AddVendorViaAccessCodeAsync(AddVendor addVendor, CancellationToken cancellationToken);

    /// <summary>
    /// The user's connected banks, by name, with when each one's consent ends.
    /// </summary>
    IAsyncEnumerable<BankConnectionSummary> GetConnectionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A link that signs the user in to the connection's bank again to renew its consent, keeping the same
    /// connection. They come back with an access code for <see cref="AddVendorViaAccessCodeAsync"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The connection doesn't exist or the user doesn't own it.</exception>
    /// <exception cref="HttpRequestException">
    /// The provider refused, e.g. consent ended too long ago, so the bank has to be connected again from scratch.
    /// </exception>
    Task<string> GetRenewalUrlAsync(Guid connectionId, CancellationToken cancellationToken);
}
