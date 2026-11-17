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

    /// <param name="userId">The user it belongs to.</param>
    /// <param name="name">What the user calls it.</param>
    /// <param name="order">Where it runs among the user's rules.</param>
    /// <param name="matchMode">How many of its conditions a transaction has to meet.</param>
    /// <param name="conditions">What a transaction has to meet.</param>
    /// <param name="actions">What it does to a transaction it matches.</param>
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

    /// <summary>
    /// What the user calls it.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Where it runs among the user's rules.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether it runs.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// How many of its conditions a transaction has to meet.
    /// </summary>
    public RuleMatchMode MatchMode { get; private set; }

    /// <summary>
    /// Whether rules after this one are skipped for a transaction it matches.
    /// </summary>
    public bool StopProcessing { get; private set; }

    /// <summary>
    /// What a transaction has to meet.
    /// </summary>
    public IReadOnlyList<RuleCondition> Conditions => _conditions.AsReadOnly();

    /// <summary>
    /// What it does to a transaction it matches.
    /// </summary>
    public IReadOnlyList<RuleAction> Actions => _actions.AsReadOnly();

    /// <summary>
    /// Changes what the user calls it.
    /// </summary>
    /// <param name="name">The new name.</param>
    [MemberNotNull(nameof(Name))]
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// Changes where it runs among the user's rules.
    /// </summary>
    /// <param name="order">Its place, 0 or more.</param>
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
            throw new DomainRuleException("A rule needs an action before it can be enabled.");
        }

        IsEnabled = true;
    }

    /// <summary>
    /// Stops it running.
    /// </summary>
    public void Disable() => IsEnabled = false;

    /// <summary>
    /// Changes how many of its conditions a transaction has to meet.
    /// </summary>
    /// <param name="matchMode">The new mode.</param>
    public void ChangeMatchMode(RuleMatchMode matchMode)
    {
        if (!Enum.IsDefined(matchMode))
        {
            throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, "Unknown match mode.");
        }

        MatchMode = matchMode;
    }

    /// <summary>
    /// Sets whether rules after this one are skipped for a transaction it matches.
    /// </summary>
    /// <param name="stopProcessing">Whether to skip them.</param>
    public void StopProcessingAfterMatch(bool stopProcessing) => StopProcessing = stopProcessing;

    /// <param name="conditions">What a transaction has to meet now.</param>
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
    /// <param name="actions">What it does now.</param>
    /// <exception cref="ArgumentException">There are no actions, more than one decides the transfer answer or pays a
    /// bill, or one that hides a bank classification or pays a bill doesn't say which.</exception>
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

        List<RuleAction> paysBill = replacements.Where(static x => x.Type == RuleActionType.PayBill).ToList();

        if (paysBill.Count > 1)
        {
            throw new ArgumentException("A rule can only pay one bill.", nameof(actions));
        }

        if (paysBill.Exists(static x => x.BillId is null))
        {
            throw new ArgumentException("Paying a bill needs to say which.", nameof(actions));
        }

        List<RuleAction> paysDebt = replacements.Where(static x => x.Type == RuleActionType.PayDebt).ToList();

        if (paysDebt.Count > 1)
        {
            throw new ArgumentException("A rule can only pay off one debt.", nameof(actions));
        }

        if (paysDebt.Exists(static x => x.DebtId is null))
        {
            throw new ArgumentException("Paying off a debt needs to say which.", nameof(actions));
        }

        _actions.Clear();
        _actions.AddRange(replacements);
    }

    /// <summary>
    /// Whether applying this rule to <paramref name="transaction"/> could change. It matches now, or it tagged or
    /// decided it before and may need to take that back.
    /// </summary>
    /// <param name="transaction">The transaction to check.</param>
    public bool Reaches(BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return Matches(transaction) || transaction.WasDecidedBy(Id);
    }

    /// <summary>
    /// Drops the actions that add a tag being deleted. A rule left with nothing to do is disabled rather than deleted,
    /// so the user can see it and give it a new action.
    /// </summary>
    /// <param name="classificationId">The tag being deleted.</param>
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
    /// Drops the action that pays a bill being deleted. A rule left with nothing to do is disabled rather than deleted,
    /// see <see cref="RemoveTag"/>.
    /// </summary>
    /// <param name="billId">The bill being deleted.</param>
    /// <returns>Whether the rule paid the bill.</returns>
    public bool RemoveBill(Guid billId)
    {
        int removed = _actions.RemoveAll(x => x.Type == RuleActionType.PayBill && x.BillId == billId);

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
    /// Drops the action that pays off a debt being deleted. A rule left with nothing to do is disabled rather than
    /// deleted, see <see cref="RemoveTag"/>.
    /// </summary>
    /// <param name="debtId">The debt being deleted.</param>
    /// <returns>Whether the rule paid off the debt.</returns>
    public bool RemoveDebt(Guid debtId)
    {
        int removed = _actions.RemoveAll(x => x.Type == RuleActionType.PayDebt && x.DebtId == debtId);

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
    /// <param name="transaction">The transaction to check.</param>
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
