using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainSavingsGoal = FinanceTracker.Domain.SavingsGoals.SavingsGoal;
using SavingsGoal = FinanceTracker.Contracts.SavingsGoals.SavingsGoal;

namespace FinanceTracker.Infrastructure.SavingsGoals;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISavingsGoalsService>]
public class SavingsGoalsService : ServiceBase<SavingsGoalsService>, ISavingsGoalsService
{
    public SavingsGoalsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<SavingsGoalsService> logger) : base(currentUser,
        financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<SavingsGoal> GetSavingsGoalsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (DomainSavingsGoal savingsGoal in context.SavingsGoals.OwnedBy(UserId)
                           .AsNoTracking()
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new SavingsGoal
            {
                Id = savingsGoal.Id,
                Name = savingsGoal.Name,
                Saved = savingsGoal.Saved,
                MonthlyContribution = savingsGoal.MonthlyContribution,
                Target = savingsGoal.Target,
                TargetDate = savingsGoal.TargetDate
            };
        }
    }

    public async Task<SavingsGoal> AddSavingsGoalAsync(AddSavingsGoal goalToAdd,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DomainSavingsGoal savingsGoal = new(UserId, goalToAdd.Name,
            goalToAdd.Saved, goalToAdd.MonthlyContribution, goalToAdd.Target,
            goalToAdd.TargetDate);

        context.SavingsGoals.Add(savingsGoal);

        await context.SaveChangesAsync(cancellationToken);

        return new SavingsGoal
        {
            Id = savingsGoal.Id,
            Name = savingsGoal.Name,
            Saved = savingsGoal.Saved,
            MonthlyContribution = savingsGoal.MonthlyContribution,
            Target = savingsGoal.Target,
            TargetDate = savingsGoal.TargetDate
        };
    }

    public async Task<bool> DeleteSavingsGoalAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DomainSavingsGoal? savingsGoal = await context.SavingsGoals.OwnedBy(UserId)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (savingsGoal == null)
        {
            return false;
        }

        context.SavingsGoals.Remove(savingsGoal);
        await context.SaveChangesAsync(cancellationToken);
        return true;

    }
}
