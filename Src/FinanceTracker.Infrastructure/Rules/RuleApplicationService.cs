using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Rules;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IRuleApplicationService>]
public class RuleApplicationService : ServiceBase<RuleApplicationService>, IRuleApplicationService
{
    private readonly ITransferMatchingService _transferMatchingService;
    private readonly TimeProvider _timeProvider;
    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    public RuleApplicationService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<RuleApplicationService> logger,
        ITransferMatchingService transferMatchingService, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _transferMatchingService = transferMatchingService;
        _timeProvider = timeProvider;
    }

    public async Task<int> ApplyChangesAsync(DateTime firstRunFrom, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DateTime startedAt = Now;

        RuleCheckpoint? checkpoint = await context.RuleCheckpoints.OwnedBy(UserId)
            .SingleOrDefaultAsync(cancellationToken);
        DateTime since = checkpoint?.AppliedFrom ?? firstRunFrom;

        List<Rule> rules = await LoadEnabledRulesAsync(context, cancellationToken);
        int changed = 0;

        // Without rules there's nothing to apply, but the checkpoint still moves on so enabling one later doesn't
        // reach back over the transactions synced in the meantime
        if (rules.Count > 0)
        {
            List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId)
                .Where(x => x.Created > since || x.Updated > since)
                .ToListAsync(cancellationToken);

            changed = Apply(rules, transactions);
        }

        if (checkpoint is null)
        {
            context.RuleCheckpoints.Add(new RuleCheckpoint(UserId, startedAt));
        }
        else
        {
            checkpoint.Record(startedAt);
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogAppliedRules(UserId, rules.Count, changed);

        return changed;
    }

    public async Task<int> ReapplyAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId)
            .Where(x => x.RuleTags.Any(t => t.RuleId == ruleId) ||
                        (x.Transfer != null && x.Transfer.RuleId == ruleId))
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
        {
            return 0;
        }

        // Taken before applying, the rule's transfer answers may be about to go
        List<Guid> decidedByTheRule = transactions
            .Where(x => x.Transfer?.RuleId == ruleId)
            .Select(static x => x.Id)
            .ToList();

        List<Rule> rules = await LoadEnabledRulesAsync(context, cancellationToken);
        int changed = Apply(rules, transactions);

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogReappliedRules(UserId, ruleId, transactions.Count, changed);

        if (decidedByTheRule.Count > 0)
        {
            await _transferMatchingService.MatchAroundAsync(decidedByTheRule, cancellationToken);
        }

        return changed;
    }

    public async Task<int> ApplyToExistingAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        Rule rule = await context.Rules.OwnedBy(UserId).AsNoTracking()
            .SingleAsync(x => x.Id == ruleId, cancellationToken);
        if (!rule.IsEnabled)
        {
            return 0;
        }

        // Conditions test encrypted fields, so every transaction is loaded and checked once decrypted
        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId)
            .ToListAsync(cancellationToken);

        List<BankTransaction> reached = transactions.Where(rule.Reaches).ToList();

        Dictionary<Guid, TransferInfo?> transfersBefore =
            reached.ToDictionary(static x => x.Id, static x => x.Transfer);

        List<Rule> rules = await LoadEnabledRulesAsync(context, cancellationToken);
        int changed = Apply(rules, reached);

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogAppliedRuleToExisting(UserId, ruleId, reached.Count, changed);

        List<Guid> transferChanged = reached
            .Where(x => TransferChanged(transfersBefore[x.Id], x.Transfer))
            .Select(static x => x.Id)
            .ToList();

        if (transferChanged.Count > 0)
        {
            await _transferMatchingService.MatchAroundAsync(transferChanged, cancellationToken);
        }

        return changed;
    }

    private static bool TransferChanged(TransferInfo? before, TransferInfo? after)
    {
        if (before is null)
        {
            return after is not null;
        }

        return !before.IsSameAs(after);
    }

    private async Task<List<Rule>> LoadEnabledRulesAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken) =>
        await context.Rules.OwnedBy(UserId)
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken);

    private int Apply(List<Rule> rules, List<BankTransaction> transactions)
    {
        DateTime decidedAt = Now;
        int changed = 0;

        foreach (BankTransaction transaction in transactions)
        {
            if (RuleEvaluator.Apply(rules, transaction, decidedAt))
            {
                changed++;
            }
        }

        return changed;
    }
}
