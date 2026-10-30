using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Rules;

/// <summary>
/// A user defined rule when a transaction meets its <see cref="Conditions"/>, its <see cref="Actions"/> are applied. Rules
/// run in <see cref="Order"/>, see <see cref="RuleEvaluator"/>.
/// </summary>
public sealed class Rule : AggregateRoot
{
    private readonly List<RuleCondition> _conditions = [];
    private readonly List<RuleAction> _actions = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private Rule()
    {
    }
#pragma warning restore CS8618

    /// <exception cref="ArgumentException">There are no conditions or no actions, or the actions conflict, see
    /// <see cref="ReplaceActions"/>.</exception>
    public Rule(Guid userId, string name, int order, RuleMatchMode matchMode, IEnumerable<RuleCondition> conditions,
        IEnumerable<RuleAction> actions) : base(userId)
    {
        Rename(name);
        MoveTo(order);
        ChangeMatchMode(matchMode);
        ReplaceConditions(conditions);
        ReplaceActions(actions);
        IsEnabled = true;
    }

    public string Name { get; private set; }

    /// <summary>
    /// Where it runs among the user's rules.
    /// </summary>
    public int Order { get; private set; }

    public bool IsEnabled { get; private set; }

    public RuleMatchMode MatchMode { get; private set; }

    /// <summary>
    /// Whether rules after this one are skipped for a transaction it matches.
    /// </summary>
    public bool StopProcessing { get; private set; }

    public IReadOnlyList<RuleCondition> Conditions => _conditions.AsReadOnly();

    public IReadOnlyList<RuleAction> Actions => _actions.AsReadOnly();

    [MemberNotNull(nameof(Name))]
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void MoveTo(int order)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        Order = order;
    }

    /// <exception cref="InvalidOperationException">The rule has no actions left, see <see cref="RemoveTag"/>.
    /// </exception>
    public void Enable()
    {
        if (_actions.Count == 0)
        {
            throw new InvalidOperationException("A rule needs an action before it can be enabled.");
        }

        IsEnabled = true;
    }

    public void Disable() => IsEnabled = false;

    public void ChangeMatchMode(RuleMatchMode matchMode)
    {
        if (!Enum.IsDefined(matchMode))
        {
            throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, "Unknown match mode.");
        }

        MatchMode = matchMode;
    }

    public void StopProcessingAfterMatch(bool stopProcessing) => StopProcessing = stopProcessing;

    /// <exception cref="ArgumentException">There are no conditions.</exception>
    public void ReplaceConditions(IEnumerable<RuleCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        List<RuleCondition> replacements = conditions.ToList();
        if (replacements.Count == 0)
        {
            throw new ArgumentException("A rule needs at least one condition.", nameof(conditions));
        }

        _conditions.Clear();
        _conditions.AddRange(replacements);
    }

    /// <summary>
    /// Replaces the actions, adding each tag once.
    /// </summary>
    /// <exception cref="ArgumentException">There are no actions, more than one decides the transfer answer, or one
    /// that hides a bank classification doesn't say which.</exception>
    public void ReplaceActions(IEnumerable<RuleAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        List<RuleAction> replacements = actions.Distinct().ToList();
        if (replacements.Count == 0)
        {
            throw new ArgumentException("A rule needs at least one action.", nameof(actions));
        }

        if (replacements.Count(static x => x.DecidesTransfer) > 1)
        {
            throw new ArgumentException("A rule can only decide the transfer answer once.", nameof(actions));
        }

        if (replacements.Exists(static x =>
                x.Type == RuleActionType.HideClassification && string.IsNullOrWhiteSpace(x.BankClassification)))
        {
            throw new ArgumentException("Hiding a bank classification needs to say which.", nameof(actions));
        }

        _actions.Clear();
        _actions.AddRange(replacements);
    }

    /// <summary>
    /// Whether applying this rule to <paramref name="transaction"/> could change. It matches now, or it tagged or
    /// decided it before and may need to take that back.
    /// </summary>
    public bool Reaches(BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return Matches(transaction) || transaction.WasDecidedBy(Id);
    }

    /// <summary>
    /// Drops the actions that add a tag being deleted. A rule left with nothing to do is disabled rather than deleted,
    /// so the user can see it and give it a new action.
    /// </summary>
    /// <returns>Whether the rule added the tag.</returns>
    public bool RemoveTag(Guid classificationId)
    {
        int removed = _actions.RemoveAll(x =>
            x.Type == RuleActionType.AddTag && x.ClassificationId == classificationId);

        if (removed == 0)
        {
            return false;
        }

        if (_actions.Count == 0)
        {
            Disable();
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="transaction"/> meets all or any of the conditions, by <see cref="MatchMode"/>. Doesn't
    /// look at <see cref="IsEnabled"/>, so a disabled rule can still be previewed.
    /// </summary>
    public bool Matches(BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (MatchMode == RuleMatchMode.Any)
        {
            return _conditions.Any(x => x.Matches(transaction));
        }

        return _conditions.All(x => x.Matches(transaction));
    }
}
