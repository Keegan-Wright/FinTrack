using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Classification;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
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

        await EnsureTagIsFreeAsync(context, classification.Tag, null, cancellationToken);

        CustomClassification newClassification = new(UserId, classification.Tag);
        context.CustomClassifications.Add(newClassification);

        await context.SaveChangesAsync(cancellationToken);

        return new Contracts.Classifications.Classification { Tag = newClassification.Tag, ClassificationId = newClassification.Id };
    }

    public async Task<Contracts.Classifications.Classification> RenameCustomClassificationAsync(Guid id,
        RenameClassification request, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        CustomClassification classification = await context.CustomClassifications.OwnedBy(UserId)
            .SingleAsync(x => x.Id == id, cancellationToken);
        await EnsureTagIsFreeAsync(context, request.Tag, id, cancellationToken);
        classification.Rename(request.Tag);

        await context.SaveChangesAsync(cancellationToken);

        return new Contracts.Classifications.Classification { Tag = classification.Tag, ClassificationId = classification.Id };
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
            transaction.RemoveDeletedClassification(id);
        }

        List<Rule> rules = await context.Rules.OwnedBy(UserId).ToListAsync(cancellationToken);
        foreach (Rule rule in rules)
        {
            rule.RemoveTag(id);
        }

        List<Budget> budgets = await context.Budgets.OwnedBy(UserId).ToListAsync(cancellationToken);
        foreach (Budget budget in budgets)
        {
            budget.RemoveTag(id);
        }

        List<SavingsGoal> goals = await context.SavingsGoals.OwnedBy(UserId).ToListAsync(cancellationToken);
        foreach (SavingsGoal goal in goals)
        {
            goal.RemoveTag(id);
        }

        context.CustomClassifications.Remove(classificationToRemove);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Throws when another of the user's tags already has <paramref name="tag"/> as its name, in any case, so
    /// "holiday" and "Holiday" can't both exist.
    /// </summary>
    /// <param name="exceptId">The tag being renamed, which can keep its own name in a different case.</param>
    private async Task EnsureTagIsFreeAsync(FinanceTrackerContext context, string tag, Guid? exceptId,
        CancellationToken cancellationToken)
    {
        // Tags are encrypted, so they're compared once decrypted
        List<CustomClassification> existing = await context.CustomClassifications.OwnedBy(UserId).AsNoTracking()
            .ToListAsync(cancellationToken);

        bool taken = existing.Any(x => x.Id != exceptId &&
                                       string.Equals(x.Tag.Trim(), tag.Trim(), StringComparison.OrdinalIgnoreCase));
        if (taken)
        {
            throw new DuplicateTagException(tag.Trim());
        }
    }
}
