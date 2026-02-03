using FinanceTracker.Enums;
using FinanceTracker.Models.External;
using FinanceTracker.Models.Request.OpenBanking;

namespace FinanceTracker.Services.OpenBanking;

public interface IOpenBankingService
{
    IAsyncEnumerable<ExternalOpenBankingProvider> GetOpenBankingProvidersForClientAsync(CancellationToken cancellationToken);
    string BuildAuthUrl(GetProviderSetupUrlRequestModel setupProviderRequestModel, CancellationToken cancellationToken);
    Task<bool> AddVendorViaAccessCodeAsync(string accessCode, CancellationToken cancellationToken);
    Task PerformSyncAsync(SyncTypes syncFlags, CancellationToken cancellationToken);
}