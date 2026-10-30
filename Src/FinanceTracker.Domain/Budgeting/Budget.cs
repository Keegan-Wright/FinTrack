using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Budgeting;

/// <summary>
/// A monthly spending limit, e.g. £400 on groceries, over the transactions carrying any of its <see cref="TagIds"/>.
/// Rules do the tagging. A budget is only what it covers and how much.
/// </summary>
/// <remarks>
/// Its limit is kept per month from when each one was set, see <see cref="ChangeLimit"/>, so past months keep the limit
/// they had. What's spent is never stored, <see cref="BudgetCalculator"/> works it out from the transactions.
/// </remarks>
public sealed class Budget : AggregateRoot
{
    private readonly List<Guid> _tagIds = [];
    private readonly List<BudgetLimit> _limits = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private Budget()
    {
    }
#pragma warning restore CS8618

    /// <param name="from">The first month it runs.</param>
    /// <exception cref="ArgumentException">There are no tags, or the limit is negative.</exception>
    public Budget(Guid userId, string name, IEnumerable<Guid> tagIds, decimal monthlyLimit, BudgetMonth from,
        RolloverMode rolloverMode) : base(userId)
    {
        Rename(name);
        ReplaceTags(tagIds);
        ChangeRolloverMode(rolloverMode);
        ChangeLimit(monthlyLimit, from);
    }

    public string Name { get; private set; }

    /// <summary>
    /// The user's tags it covers, a transaction with any of them counts towards it.
    /// </summary>
    public IReadOnlyCollection<Guid> TagIds => _tagIds.AsReadOnly();

    /// <summary>
    /// Its limits oldest first, each applying from its month until the next.
    /// </summary>
    public IReadOnlyList<BudgetLimit> Limits => _limits.AsReadOnly();

    public RolloverMode RolloverMode { get; private set; }

    /// <summary>
    /// Whether all of its tags have been deleted, so it covers nothing until it's given one.
    /// </summary>
    public bool CoversNothing => _tagIds.Count == 0;

    /// <summary>
    /// The first month it runs, before which it has no limit.
    /// </summary>
    public BudgetMonth FirstMonth => _limits[0].Month;

    [MemberNotNull(nameof(Name))]
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <exception cref="ArgumentException">There are no tags.</exception>
    public void ReplaceTags(IEnumerable<Guid> tagIds)
    {
        ArgumentNullException.ThrowIfNull(tagIds);

        List<Guid> replacements = tagIds.Distinct().ToList();
        if (replacements.Count == 0)
        {
            throw new ArgumentException("A budget needs at least one tag.", nameof(tagIds));
        }

        _tagIds.Clear();
        _tagIds.AddRange(replacements);
    }

    /// <summary>
    /// Drops a tag being deleted. A budget left with none is kept, see <see cref="CoversNothing"/>, so the user can
    /// give it another rather than lose its history.
    /// </summary>
    /// <returns>Whether the budget covered the tag.</returns>
    public bool RemoveTag(Guid tagId) => _tagIds.Remove(tagId);

    public void ChangeRolloverMode(RolloverMode rolloverMode)
    {
        if (!Enum.IsDefined(rolloverMode))
        {
            throw new ArgumentOutOfRangeException(nameof(rolloverMode), rolloverMode, "Unknown rollover mode.");
        }

        RolloverMode = rolloverMode;
    }

    /// <summary>
    /// Sets the limit from <paramref name="from"/> onwards, replacing any set for that month or later. The months
    /// before keep theirs. A month before the first starts the budget earlier.
    /// </summary>
    /// <returns>Whether the limit changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public bool ChangeLimit(decimal amount, BudgetMonth from)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (LimitFor(from) == amount && !_limits.Exists(x => x.Month > from))
        {
            return false;
        }

        _limits.RemoveAll(x => x.Month >= from);
        _limits.Add(new BudgetLimit(from.FirstDay, amount));
        _limits.Sort(static (left, right) => left.From.CompareTo(right.From));
        return true;
    }

    /// <summary>
    /// The limit for <paramref name="month"/>, null before the budget's first month.
    /// </summary>
    public decimal? LimitFor(BudgetMonth month)
    {
        BudgetLimit? limit = _limits.LastOrDefault(x => x.Month <= month);
        return limit?.Amount;
    }

    /// <summary>
    /// Whether <paramref name="transaction"/> carries any of the budget's tags. Transfers never count, see
    /// <see cref="BankTransaction.CountsAsIncomeOrSpending"/>.
    /// </summary>
    public bool Covers(BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction.CountsAsIncomeOrSpending && transaction.CustomClassificationIds.Any(_tagIds.Contains);
    }
}
