using FinanceTracker.Contracts.Classifications;

namespace FinanceTracker.Infrastructure.Classification;

public interface IClassificationService
{
    IAsyncEnumerable<Contracts.Classifications.Classification> GetAllCustomClassificationsAsync(CancellationToken cancellationToken);
    Task<GetClassification> GetClassificationAsync(Guid id, CancellationToken cancellationToken);

    Task<Contracts.Classifications.Classification> AddCustomClassificationAsync(AddClassifications classification,
        CancellationToken cancellationToken);

    Task AddCustomClassificationsToTransactionAsync(AddCustomClassificationsToTransaction model,
        CancellationToken cancellationToken);

    Task RemoveCustomClassificationAsync(Guid id, CancellationToken cancellationToken);
}
