using FinanceTracker.Contracts.Classifications;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's custom classifications (tags), and applying them to transactions.
/// </summary>
/// <remarks>
/// Methods that look up by id throw <see cref="InvalidOperationException"/> when the id doesn't exist or belongs to
/// another user, so one user can't read or change another's classifications.
/// </remarks>
public interface IClassificationService
{
    /// <summary>
    /// All of the user's custom classifications, in no particular order.
    /// </summary>
    IAsyncEnumerable<Classification> GetAllCustomClassificationsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One of the user's custom classifications.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no classification with <paramref name="id"/>.</exception>
    Task<GetClassification> GetClassificationAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a custom classification for the user. Validate the request with
    /// <see cref="Validation.AddClassificationsRequestValidator"/> first.
    /// </summary>
    /// <returns>The classification as stored, with its new id.</returns>
    Task<Classification> AddCustomClassificationAsync(AddClassifications classification,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies classifications to one of the user's transactions. Classifications the user doesn't own are
    /// skipped, ones already applied are left as they are. Validate the request with
    /// <see cref="Validation.AddCustomClassificationsToTransactionRequestValidator"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no transaction with the request's id.</exception>
    Task AddCustomClassificationsToTransactionAsync(AddCustomClassificationsToTransaction model,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's custom classifications and removes it from every transaction it was applied to.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no classification with <paramref name="id"/>.</exception>
    Task RemoveCustomClassificationAsync(Guid id, CancellationToken cancellationToken);
}
