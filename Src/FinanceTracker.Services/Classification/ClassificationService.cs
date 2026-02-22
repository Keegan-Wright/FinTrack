using System.Runtime.CompilerServices;
using System.Security.Claims;
using EFCore.BulkExtensions;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Request.Classifications;
using FinanceTracker.Models.Response.Classifications;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Classification;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IClassificationService>]
public class ClassificationService : ServiceBase, IClassificationService
{
    public ClassificationService(ClaimsPrincipal user,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory) : base(user,
        financeTrackerContextFactory)
    {
    }

    public async IAsyncEnumerable<ClassificationsResponse> GetAllCustomClassificationsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IAsyncEnumerable<CustomClassification> classifications = context.IsolateToUser(UserId)
            .Include(x => x.CustomClassifications)
            .SelectMany(x => x.CustomClassifications).AsAsyncEnumerable();

        await foreach (CustomClassification classification in classifications.WithCancellation(cancellationToken))
        {
            yield return new ClassificationsResponse { Tag = classification.Tag, ClassificationId = classification.Id };
        }
    }

    public async Task<GetClassificationResponse> GetClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        CustomClassification classification = await context.IsolateToUser(UserId)
            .Include(x => x.CustomClassifications)
            .SelectMany(x => x.CustomClassifications)
            .SingleAsync(x => x.Id == id, cancellationToken);

        return new GetClassificationResponse { Tag = classification.Tag, ClassificationId = classification.Id };
    }

    public async Task<ClassificationsResponse> AddCustomClassificationAsync(AddClassificationsRequest classification,
        CancellationToken cancellationToken)
    {
        CustomClassification newClassification = new() { Tag = classification.Tag };
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        FinanceTrackerUser user = await context.IsolateToUser(UserId).Include(x => x.CustomClassifications)
            .SingleAsync(cancellationToken);

        user.CustomClassifications.Add(newClassification);

        await context.SaveChangesAsync(cancellationToken);

        return new ClassificationsResponse { Tag = classification.Tag, ClassificationId = newClassification.Id };
    }

    public async Task AddCustomClassificationsToTransactionAsync(
        AddCustomClassificationsToTransactionRequest requestModel,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<FinanceTrackerUser> query = context.IsolateToUser(UserId);


        OpenBankingTransaction? transaction = await query
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.Transactions)
            .ThenInclude(x => x.Classifications)
            .SelectMany(x => x.Providers.SelectMany(c => c.Accounts).SelectMany(r => r.Transactions))
            .FirstOrDefaultAsync(x => x.Id == requestModel.TransactionId, cancellationToken);

        IAsyncEnumerable<CustomClassification> classifications = query
            .Include(x => x.CustomClassifications)
            .SelectMany(x => x.CustomClassifications)
            .Where(x => requestModel.Classifications.Select(c => c.ClassificationId).Contains(x.Id))
            .ToAsyncEnumerable();

        List<OpenBankingTransactionClassifications> newClassifications = new();
        await foreach (CustomClassification classification in classifications.WithCancellation(cancellationToken))
        {
            OpenBankingTransactionClassifications newClassification = new()
            {
                Transaction = transaction,
                TransactionId = transaction.Id,
                Classification = classification.Tag,
                IsCustomClassification = true
            };
            newClassifications.Add(newClassification);
        }

        foreach (OpenBankingTransactionClassifications classification in newClassifications)
        {
            transaction.Classifications.Add(classification);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveCustomClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<FinanceTrackerUser> query = context.IsolateToUser(UserId);


        CustomClassification classificationToRemove =
            await query.Include(x => x.CustomClassifications)
                .SelectMany(x => x.CustomClassifications)
                .SingleAsync(x => x.Id == id, cancellationToken);


        List<OpenBankingTransactionClassifications> transactionClassifications = await query
            .Include(x => x.Providers)
            .ThenInclude(x => x.Accounts)
            .ThenInclude(x => x.Transactions)
            .ThenInclude(x => x.Classifications)
            .SelectMany(x =>
                x.Providers.SelectMany(c => c.Accounts).SelectMany(r => r.Transactions)
                    .SelectMany(x => x.Classifications))
            .Where(x => x.IsCustomClassification == true && x.Classification == classificationToRemove.Tag)
            .ToListAsync(cancellationToken);

        await context.BulkDeleteAsync(transactionClassifications, cancellationToken: cancellationToken);
        await context.BulkDeleteAsync([classificationToRemove], cancellationToken: cancellationToken);
    }
}
