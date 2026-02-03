using System.Security.Claims;
using FinanceTracker.Data;
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

public class OpenBankingService : ServiceBase, IOpenBankingService
{
    public OpenBankingService(ClaimsPrincipal user, FinanceTrackerContext financeTrackerContext) : base(user, financeTrackerContext)
    {
    }

    public IAsyncEnumerable<ExternalOpenBankingProvider> GetOpenBankingProvidersForClientAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public string BuildAuthUrl(GetProviderSetupUrlRequestModel setupProviderRequestModel, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> AddVendorViaAccessCodeAsync(string accessCode, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task PerformSyncAsync(SyncTypes syncFlags, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}