using FinanceTracker.Contracts;
using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Domain;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Infrastructure.OpenBanking;

public interface IOpenBankingService : IServiceBase
{
    IAsyncEnumerable<ExternalOpenBankingProvider> GetOpenBankingProvidersForClientAsync(
        CancellationToken cancellationToken);

    string BuildAuthUrl(GetProviderSetupUrlRequestModel setupProviderRequestModel);
    Task<bool> AddVendorViaAccessCodeAsync(AddVendorRequestModel addVendorRequestModel, CancellationToken cancellationToken);
    Task PerformSyncAsync(SyncTypes syncFlags, CancellationToken cancellationToken);
    Task BulkLoadProviderAsync(OpenBankingProvider provider, SyncTypes syncFlags, CancellationToken cancellationToken);

    async Task RunFunctionAsUser(Guid userId, Func<Task> action)
    {
        SetAutomationInstanceUserId(userId);
        await action();
        SetAutomationInstanceUserId(Guid.Empty);
    }
}
