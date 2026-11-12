using DomainRule = FinanceTracker.Domain.Rules.Rule;

namespace FinanceTracker.Infrastructure.Rules;

/// <summary>
/// A rule another person chose to share with the user, see <see cref="SharedRules"/>.
/// </summary>
/// <param name="Rule">The rule, its owner's.</param>
/// <param name="SharedBy">Its owner's first and last name.</param>
/// <param name="CanChange">Whether the user can edit it.</param>
internal sealed record SharedRule(DomainRule Rule, string SharedBy, bool CanChange);
