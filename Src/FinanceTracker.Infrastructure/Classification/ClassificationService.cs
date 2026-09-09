using System.Runtime.CompilerServices;
using System.Security.Claims;
using EFCore.BulkExtensions;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Domain;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Classification;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IClassificationService>]
public class ClassificationService : ServiceBase<ClassificationService>, IClassificationService
{
    public ClassificationService(ClaimsPrincipal user,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<ClassificationService> logger) : base(user,
        financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<Contracts.Classifications.Classification> GetAllCustomClassificationsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IAsyncEnumerable<CustomClassification> classifications = context.IsolateToUser(UserId)
            .Include(x => x.CustomClassifications)
            .SelectMany(x => x.CustomClassifications!).AsAsyncEnumerable();

        await foreach (CustomClassification classification in classifications.WithCancellation(cancellationToken))
        {
            yield return new Contracts.Classifications.Classification { Tag = classification.Tag, ClassificationId = classification.Id };
        }
    }

    public async Task<GetClassification> GetClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        CustomClassification classification = await context.IsolateToUser(UserId)
            .Include(x => x.CustomClassifications)
            .SelectMany(x => x.CustomClassifications!)
            .SingleAsync(x => x.Id == id, cancellationToken);

        return new GetClassification { Tag = classification.Tag, ClassificationId = classification.Id };
    }

    public async Task<Contracts.Classifications.Classification> AddCustomClassificationAsync(AddClassifications classification,
        CancellationToken cancellationToken)
    {
        CustomClassification newClassification = new() { Tag = classification.Tag };
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        FinanceTrackerUser user = await context.IsolateToUser(UserId)
            .Include(x => x.CustomClassifications)
            .SingleAsync(cancellationToken);

        user.CustomClassifications?.Add(newClassification);

        await context.SaveChangesAsync(cancellationToken);

        return new Contracts.Classifications.Classification { Tag = classification.Tag, ClassificationId = newClassification.Id };
    }

    public async Task AddCustomClassificationsToTransactionAsync(
        AddCustomClassificationsToTransaction model,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<FinanceTrackerUser> query = context.IsolateToUser(UserId);


        OpenBankingTransaction? transaction = await query
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.Transactions)!
            .ThenInclude(x => x.Classifications)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.Transactions!))
            .FirstOrDefaultAsync(x => x.Id == model.TransactionId, cancellationToken);

        IAsyncEnumerable<CustomClassification> classifications = query
            .Include(x => x.CustomClassifications)
            .SelectMany(x => x.CustomClassifications!)
            .Where(x => model.Classifications.Select(c => c.ClassificationId).Contains(x.Id))
            .ToAsyncEnumerable();

        List<OpenBankingTransactionClassifications> newClassifications = [];
        await foreach (CustomClassification classification in classifications.WithCancellation(cancellationToken))
        {
            OpenBankingTransactionClassifications newClassification = new()
            {
                Transaction = transaction,
                TransactionId = transaction!.Id,
                Classification = classification.Tag,
                IsCustomClassification = true
            };
            newClassifications.Add(newClassification);
        }

        foreach (OpenBankingTransactionClassifications classification in newClassifications)
        {
            transaction!.Classifications!.Add(classification);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveCustomClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<FinanceTrackerUser> query = context.IsolateToUser(UserId);


        CustomClassification classificationToRemove =
            await query.Include(x => x.CustomClassifications)
                .SelectMany(x => x.CustomClassifications!)
                .SingleAsync(x => x.Id == id, cancellationToken);


        List<OpenBankingTransactionClassifications> transactionClassifications = await query
            .Include(x => x.Providers)!
            .ThenInclude(x => x.Accounts)!
            .ThenInclude(x => x.Transactions)!
            .ThenInclude(x => x.Classifications)
            .SelectMany(x =>
                x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.Transactions!)
                    .SelectMany(v => v.Classifications!))
            .Where(x => x.IsCustomClassification == true && x.Classification == classificationToRemove.Tag)
            .ToListAsync(cancellationToken);

        await context.BulkDeleteAsync(transactionClassifications, cancellationToken: cancellationToken);
        await context.BulkDeleteAsync([classificationToRemove], cancellationToken: cancellationToken);
    }
}
