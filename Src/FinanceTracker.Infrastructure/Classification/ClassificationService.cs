using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Classification;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IClassificationService>]
public class ClassificationService : ServiceBase<ClassificationService>, IClassificationService
{
    private readonly IAccountScope _accountScope;
    private readonly TimeProvider _timeProvider;

    public ClassificationService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<ClassificationService> logger,
        IAccountScope accountScope, TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _accountScope = accountScope;
        _timeProvider = timeProvider;
    }

    public async IAsyncEnumerable<Contracts.Classifications.Classification> GetAllCustomClassificationsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        List<CustomClassification> own = await context.CustomClassifications.OwnedBy(UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (CustomClassification classification in own)
        {
            yield return new Contracts.Classifications.Classification
            {
                Tag = classification.Tag, ClassificationId = classification.Id
            };
        }

        // Those another person chose to share with them, which they can use too
        foreach ((CustomClassification classification, string sharedBy, bool canChange) in
                 await SharedWithThemAsync(context, cancellationToken))
        {
            yield return new Contracts.Classifications.Classification
            {
                Tag = classification.Tag,
                ClassificationId = classification.Id,
                SharedBy = sharedBy,
                CanChange = canChange
            };
        }
    }

    public async Task<GetClassification> GetClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<Guid> shared = ChosenSharedIds(context);
        CustomClassification classification = await context.CustomClassifications.AsNoTracking()
            .Where(x => x.UserId == UserId || shared.Contains(x.Id))
            .SingleAsync(x => x.Id == id, cancellationToken);

        return new GetClassification { Tag = classification.Tag, ClassificationId = classification.Id };
    }

    public async Task<Contracts.Classifications.Classification> AddCustomClassificationAsync(AddClassifications classification,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        await EnsureTagIsFreeAsync(context, UserId, classification.Tag, null, cancellationToken);

        CustomClassification newClassification = new(UserId, classification.Tag);
        context.CustomClassifications.Add(newClassification);

        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.Tag, classification.Tag,
            cancellationToken);
        Logger.LogChanged(UserId, "created", "tag", newClassification.Id);

        return new Contracts.Classifications.Classification { Tag = newClassification.Tag, ClassificationId = newClassification.Id };
    }

    public async Task<Contracts.Classifications.Classification> RenameCustomClassificationAsync(Guid id,
        RenameClassification request, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one shared with them with changes allowed, kept apart from its owner's other tags
        IQueryable<Guid> changeable = context.ChosenSharesWith(UserId, ShareItemType.Classification)
            .Where(static x => x.Mode == ShareMode.AllowChanges)
            .Select(static x => x.ItemId);
        CustomClassification classification = await context.CustomClassifications
            .Where(x => x.UserId == UserId || changeable.Contains(x.Id))
            .SingleAsync(x => x.Id == id, cancellationToken);
        await EnsureTagIsFreeAsync(context, classification.UserId, request.Tag, id, cancellationToken);
        classification.Rename(request.Tag);

        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.Tag, request.Tag, cancellationToken);
        Logger.LogChanged(UserId, "renamed", "tag", id);

        return new Contracts.Classifications.Classification { Tag = classification.Tag, ClassificationId = classification.Id };
    }

    public async Task AddCustomClassificationsToTransactionAsync(
        AddCustomClassificationsToTransaction model,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one on an account shared with them with changes allowed, tagged with their own tags or those
        // shared with them. The owner is shared their own, so they see what they're called
        AccountsInView changeable = await _accountScope.ResolveChangeableAsync(context, cancellationToken);
        BankTransaction transaction = await context.BankTransactions.ChangeableIn(changeable)
            .SingleAsync(x => x.Id == model.TransactionId, cancellationToken);

        List<Guid> classificationIds = model.Classifications.Select(static x => x.ClassificationId).ToList();
        List<Guid> sharedWithThem = await ChosenSharedIds(context).ToListAsync(cancellationToken);
        List<CustomClassification> classifications = await context.CustomClassifications
            .Where(x => classificationIds.Contains(x.Id) && (x.UserId == UserId || sharedWithThem.Contains(x.Id)))
            .ToListAsync(cancellationToken);

        foreach (CustomClassification classification in classifications)
        {
            transaction.Classify(classification, UserId, sharedWithThem);
            if (classification.UserId == UserId)
            {
                await AnnotationShares.ShareWithOwnerAsync(context, UserId, transaction,
                    ShareItemType.Classification, classification.Id, _timeProvider.UtcNow, cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "tagged", "transaction", model.TransactionId);
    }

    public async Task RemoveCustomClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        CustomClassification classificationToRemove = await context.CustomClassifications.OwnedBy(UserId)
            .SingleAsync(x => x.Id == id, cancellationToken);

        // Whoever's transaction it's on, they may have let the user tag theirs
        List<BankTransaction> classifiedTransactions = await context.BankTransactions
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
        Logger.LogChanged(UserId, "deleted", "tag", id);
    }

    /// <summary>
    /// The tags another person chose to share with the user. A query, to be used inside another.
    /// </summary>
    private IQueryable<Guid> ChosenSharedIds(FinanceTrackerContext context) =>
        context.ChosenSharesWith(UserId, ShareItemType.Classification).Select(static x => x.ItemId);

    /// <summary>
    /// The tags other people chose to share with the user, each with who shares it and whether they can rename it.
    /// </summary>
    private async Task<List<(CustomClassification Tag, string SharedBy, bool CanChange)>> SharedWithThemAsync(
        FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        var shared = await (
                from share in context.ChosenSharesWith(UserId, ShareItemType.Classification)
                join tag in context.CustomClassifications on share.ItemId equals tag.Id
                join owner in context.Users on tag.UserId equals owner.Id
                where tag.UserId == share.UserId
                select new { Tag = tag, owner.FirstName, owner.LastName, share.Mode })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return shared
            .Select(static x => (x.Tag, $"{x.FirstName} {x.LastName}".Trim(), x.Mode == ShareMode.AllowChanges))
            .ToList();
    }

    /// <exception cref="NameTakenException">Another of <paramref name="ownerId"/>'s tags is called
    /// <paramref name="tag"/>.</exception>
    private static async Task EnsureTagIsFreeAsync(FinanceTrackerContext context, Guid ownerId, string tag,
        Guid? exceptId, CancellationToken cancellationToken)
    {
        List<CustomClassification> existing = await context.CustomClassifications.OwnedBy(ownerId).AsNoTracking()
            .ToListAsync(cancellationToken);
        NameCheck.EnsureFree(existing.Select(static x => (x.Id, x.Tag)), tag, exceptId, NameTakenException.Tag);
    }
}
