using FinanceTracker.Contracts.Rules;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's rules, which tag their transactions and decide transfers as they sync.
/// </summary>
/// <remarks>
/// A rule reaches the transactions that arrive or change after it's saved, never the user's history. Editing,
/// disabling or deleting one re-applies the rules to the transactions it had reached, taking back anything it no
/// longer gives. Methods that look up by id throw <see cref="InvalidOperationException"/> when the id doesn't exist or
/// belongs to another user.
/// </remarks>
public interface IRulesService
{
    /// <summary>
    /// All of the user's rules, in the order they run.
    /// </summary>
    IAsyncEnumerable<Rule> GetRulesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One of the user's rules.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no rule with <paramref name="id"/>.</exception>
    Task<Rule> GetRuleAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a rule for the user, running after their others. Validate the request with
    /// <see cref="Validation.SaveRuleValidator"/> first.
    /// </summary>
    /// <returns>The rule as stored, with its new id.</returns>
    /// <exception cref="InvalidOperationException">An action adds a tag, or a condition names an account, the user
    /// doesn't have.</exception>
    Task<Rule> AddRuleAsync(SaveRule rule, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces everything about one of the user's rules but its order. Validate the request with
    /// <see cref="Validation.SaveRuleValidator"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no rule with <paramref name="id"/>, or an action adds a
    /// tag, or a condition names an account, the user doesn't have.</exception>
    Task<Rule> UpdateRuleAsync(Guid id, SaveRule rule, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the order the user's rules run in. Validate the request with
    /// <see cref="Validation.ReorderRulesValidator"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The ids aren't exactly the user's rules.</exception>
    Task ReorderRulesAsync(ReorderRules order, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's rules and takes back the tags and transfer answers it gave.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no rule with <paramref name="id"/>.</exception>
    Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken);
}
