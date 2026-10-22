using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Classification;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IClassificationService>]
public class ClassificationService : ServiceBase<ClassificationService>, IClassificationService
{
    public ClassificationService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<ClassificationService> logger) : base(currentUser,
        financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<Contracts.Classifications.Classification> GetAllCustomClassificationsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IAsyncEnumerable<CustomClassification> classifications = context.CustomClassifications.OwnedBy(UserId)
            .AsNoTracking()
            .AsAsyncEnumerable();

        await foreach (CustomClassification classification in classifications.WithCancellation(cancellationToken))
        {
            yield return new Contracts.Classifications.Classification { Tag = classification.Tag, ClassificationId = classification.Id };
        }
    }

    public async Task<GetClassification> GetClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        CustomClassification classification = await context.CustomClassifications.OwnedBy(UserId)
            .AsNoTracking()
            .SingleAsync(x => x.Id == id, cancellationToken);

        return new GetClassification { Tag = classification.Tag, ClassificationId = classification.Id };
    }

    public async Task<Contracts.Classifications.Classification> AddCustomClassificationAsync(AddClassifications classification,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        CustomClassification newClassification = new(UserId, classification.Tag);
        context.CustomClassifications.Add(newClassification);

        await context.SaveChangesAsync(cancellationToken);

        return new Contracts.Classifications.Classification { Tag = newClassification.Tag, ClassificationId = newClassification.Id };
    }

    public async Task AddCustomClassificationsToTransactionAsync(
        AddCustomClassificationsToTransaction model,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        BankTransaction transaction = await context.BankTransactions.OwnedBy(UserId)
            .SingleAsync(x => x.Id == model.TransactionId, cancellationToken);

        List<Guid> classificationIds = model.Classifications.Select(static x => x.ClassificationId).ToList();
        List<CustomClassification> classifications = await context.CustomClassifications.OwnedBy(UserId)
            .Where(x => classificationIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (CustomClassification classification in classifications)
        {
            transaction.Classify(classification);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveCustomClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        CustomClassification classificationToRemove = await context.CustomClassifications.OwnedBy(UserId)
            .SingleAsync(x => x.Id == id, cancellationToken);

        List<BankTransaction> classifiedTransactions = await context.BankTransactions.OwnedBy(UserId)
            .Where(x => x.CustomClassificationIds.Contains(id))
            .ToListAsync(cancellationToken);

        foreach (BankTransaction transaction in classifiedTransactions)
        {
            transaction.Unclassify(id);
        }

        context.CustomClassifications.Remove(classificationToRemove);
        await context.SaveChangesAsync(cancellationToken);
    }
}
