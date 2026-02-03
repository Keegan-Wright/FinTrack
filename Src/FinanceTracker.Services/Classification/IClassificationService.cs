using FinanceTracker.Models.Request.Classifications;
using FinanceTracker.Models.Response.Classifications;

namespace FinanceTracker.Services.Classification;

public interface IClassificationService
{
    IAsyncEnumerable<ClassificationsResponse> GetAllCustomClassificationsAsync(CancellationToken cancellationToken);
    Task<GetClassificationResponse> GetClassificationAsync(Guid id, CancellationToken cancellationToken);
    Task<ClassificationsResponse> AddCustomClassificationAsync(AddClassificationsRequest classification, CancellationToken cancellationToken);
    Task AddCustomClassificationsToTransactionAsync (AddCustomClassificationsToTransactionRequest requestModel, CancellationToken cancellationToken);
    Task RemoveCustomClassificationAsync(Guid id, CancellationToken cancellationToken);
}