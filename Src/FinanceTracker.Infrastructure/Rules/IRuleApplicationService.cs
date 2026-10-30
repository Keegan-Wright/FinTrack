using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Infrastructure.Rules;

/// <summary>
/// Applies the current user's rules to their transactions, see <see cref="RuleEvaluator"/>. The background sync
/// and <see cref="Contracts.Services.IRulesService"/> call it.
/// </summary>
/// <remarks>
/// Rules only reach the transactions that arrive or change after they're saved. A user's history is only changed in
/// bulk when they ask, see <see cref="ApplyToExistingAsync"/>.
/// </remarks>
public interface IRuleApplicationService
{
    /// <summary>
    /// Applies the rules to the transactions created or updated since the last run, e.g. after a sync, pending ones
    /// included. Run before transfers are matched, so the matcher sees what the rules decided.
    /// </summary>
    /// <param name="firstRunFrom">Where the first run starts, the start of the sync that called it, so the first run
    /// covers that sync but none of the history before it.</param>
    /// <returns>How many transactions the rules changed.</returns>
    Task<int> ApplyChangesAsync(DateTime firstRunFrom, CancellationToken cancellationToken);

    /// <summary>
    /// Re-applies the rules to the transactions <paramref name="ruleId"/> tagged or decided, after it's edited,
    /// disabled or deleted, so anything it no longer gives is taken back. Transfers are then re-matched around any
    /// whose transfer answer it gave.
    /// </summary>
    /// <returns>How many transactions the rules changed.</returns>
    Task<int> ReapplyAsync(Guid ruleId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the rules to every transaction <paramref name="ruleId"/> reaches, see <see cref="Rule.Reaches"/>,
    /// however old, when the user asks. All the user's rules run on those, in order, as a sync would. Transfers are
    /// then re-matched around any whose transfer answer changed.
    /// </summary>
    /// <returns>How many transactions changed, 0 when the rule is off.</returns>
    /// <exception cref="InvalidOperationException">The user has no rule with <paramref name="ruleId"/>.</exception>
    Task<int> ApplyToExistingAsync(Guid ruleId, CancellationToken cancellationToken);
}
