using FinanceTracker.Data.Models;
using FinanceTracker.Enums;
using FinanceTracker.Models.External;
using FinanceTracker.Models.Request.OpenBanking;

namespace FinanceTracker.Services.OpenBanking;

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
