using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Budgeting;

/// <summary>
/// A monthly spending limit, e.g. £400 on groceries. It covers its <see cref="TagIds"/>, its <see cref="AccountIds"/>
/// or both. Tagged spending on any account, all spending on its accounts, or tagged spending on its accounts. Rules do
/// the tagging. A budget is only what it covers and how much.
/// </summary>
/// <remarks>
/// Its limit is kept per month from when each one was set, see <see cref="ChangeLimit"/>, so past months keep the limit
/// they had. What's spent is never stored, <see cref="BudgetCalculator"/> works it out from the transactions.
/// </remarks>
public sealed class Budget : AggregateRoot
{
    private readonly List<Guid> _tagIds = [];
    private readonly List<Guid> _accountIds = [];
    private readonly List<BudgetLimit> _limits = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private Budget()
    {
    }
#pragma warning restore CS8618

    /// <param name="userId">The user it belongs to.</param>
    /// <param name="name">What the user calls it.</param>
    /// <param name="tagIds">The user's tags it covers, none for all spending on its accounts.</param>
    /// <param name="accountIds">The accounts it covers, none for every account.</param>
    /// <param name="monthlyLimit">How much can be spent each month from <paramref name="from"/>.</param>
    /// <param name="from">The first month it runs.</param>
    /// <param name="rolloverMode">What happens to what's left or overspent at the end of a month.</param>
    /// <exception cref="ArgumentException">There are no tags and no accounts, or the limit is negative.</exception>
    public Budget(Guid userId, string name, IEnumerable<Guid> tagIds, IEnumerable<Guid> accountIds,
        decimal monthlyLimit, BudgetMonth from, RolloverMode rolloverMode) : base(userId)
    {
        Rename(name);
        SetCoverage(tagIds, accountIds);
        ChangeRolloverMode(rolloverMode);
        ChangeLimit(monthlyLimit, from);
    }

    /// <summary>
    /// What the user calls it.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The user's tags it covers, a transaction with any of them counts towards it. None for all spending on its
    /// <see cref="AccountIds"/>.
    /// </summary>
    public IReadOnlyCollection<Guid> TagIds => _tagIds.AsReadOnly();

    /// <summary>
    /// The accounts it covers, only transactions on them count towards it. None for every account.
    /// </summary>
    public IReadOnlyCollection<Guid> AccountIds => _accountIds.AsReadOnly();

    /// <summary>
    /// Its limits oldest first, each applying from its month until the next.
    /// </summary>
    public IReadOnlyList<BudgetLimit> Limits => _limits.AsReadOnly();

    /// <summary>
    /// What happens to what's left or overspent at the end of a month.
    /// </summary>
    public RolloverMode RolloverMode { get; private set; }

    /// <summary>
    /// Whether all of its tags and accounts have gone, so it covers nothing until it's given one.
    /// </summary>
    public bool CoversNothing => _tagIds.Count == 0 && _accountIds.Count == 0;

    /// <summary>
    /// The first month it runs, before which it has no limit.
    /// </summary>
    public BudgetMonth FirstMonth => _limits[0].Month;

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
    /// Covers <paramref name="tagIds"/> and <paramref name="accountIds"/> from now on, each one of the choices. Its
    /// owner's own, or for someone it's shared with, its owner's shared with them.
    /// </summary>
    /// <param name="tagIds">The tags it covers now, none for all spending on its accounts.</param>
    /// <param name="tagChoices">The tags whoever's changing it may give it.</param>
    /// <param name="accountIds">The accounts it covers now, none for every account.</param>
    /// <param name="accountChoices">The accounts whoever's changing it may give it.</param>
    /// <exception cref="ArgumentException">There are no tags and no accounts.</exception>
    /// <exception cref="DomainRuleException">A tag or account isn't one of the choices.</exception>
    public void ReplaceCoverage(IEnumerable<Guid> tagIds, IReadOnlySet<Guid> tagChoices,
        IEnumerable<Guid> accountIds, IReadOnlySet<Guid> accountChoices)
    {
        ArgumentNullException.ThrowIfNull(tagIds);
        ArgumentNullException.ThrowIfNull(tagChoices);
        ArgumentNullException.ThrowIfNull(accountIds);
        ArgumentNullException.ThrowIfNull(accountChoices);

        List<Guid> tags = tagIds.Distinct().ToList();
        if (tags.Exists(x => !tagChoices.Contains(x)))
        {
            throw new DomainRuleException(
                "A budget can only cover its owner's tags, those shared with you when it's someone else's.");
        }

        List<Guid> accounts = accountIds.Distinct().ToList();
        if (accounts.Exists(x => !accountChoices.Contains(x)))
        {
            throw new DomainRuleException(
                "A budget can only cover accounts its owner sees, those shared with you when it's someone else's.");
        }

        SetCoverage(tags, accounts);
    }

    /// <param name="tagIds">The tags it covers.</param>
    /// <param name="accountIds">The accounts it covers.</param>
    /// <exception cref="ArgumentException">There are no tags and no accounts.</exception>
    private void SetCoverage(IEnumerable<Guid> tagIds, IEnumerable<Guid> accountIds)
    {
        ArgumentNullException.ThrowIfNull(tagIds);
        ArgumentNullException.ThrowIfNull(accountIds);

        List<Guid> tags = tagIds.Distinct().ToList();
        List<Guid> accounts = accountIds.Distinct().ToList();
        if (tags.Count == 0 && accounts.Count == 0)
        {
            throw new ArgumentException("A budget needs at least one tag or account.", nameof(tagIds));
        }

        _tagIds.Clear();
        _tagIds.AddRange(tags);
        _accountIds.Clear();
        _accountIds.AddRange(accounts);
    }

    /// <summary>
    /// Drops a tag being deleted. A budget left with none is kept, see <see cref="CoversNothing"/>, so the user can
    /// give it another rather than lose its history.
    /// </summary>
    /// <param name="tagId">The tag being deleted.</param>
    /// <returns>Whether the budget covered the tag.</returns>
    public bool RemoveTag(Guid tagId) => _tagIds.Remove(tagId);

    /// <summary>
    /// Drops an account that's gone, e.g. disconnected. A budget left with no tags and no accounts is kept, see
    /// <see cref="CoversNothing"/>. One left with only tags covers them on every account.
    /// </summary>
    /// <param name="accountId">The account that's gone.</param>
    /// <returns>Whether the budget covered the account.</returns>
    public bool RemoveAccount(Guid accountId) => _accountIds.Remove(accountId);

    /// <summary>
    /// Covers <paramref name="intoAccountId"/> in place of <paramref name="mergedAccountId"/>, when a joint account's
    /// copies are merged into one.
    /// </summary>
    /// <param name="mergedAccountId">The copy merged away.</param>
    /// <param name="intoAccountId">The copy it was merged into.</param>
    /// <returns>Whether the budget covered the merged copy.</returns>
    public bool MoveAccount(Guid mergedAccountId, Guid intoAccountId)
    {
        if (!_accountIds.Remove(mergedAccountId))
        {
            return false;
        }

        if (!_accountIds.Contains(intoAccountId))
        {
            _accountIds.Add(intoAccountId);
        }

        return true;
    }

    /// <summary>
    /// Changes what happens to what's left or overspent at the end of a month.
    /// </summary>
    /// <param name="rolloverMode">The new mode.</param>
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
    /// <param name="amount">How much can be spent each month, 0 or more.</param>
    /// <param name="from">The first month it applies to.</param>
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
    /// <param name="month">The month to look at.</param>
    public decimal? LimitFor(BudgetMonth month)
    {
        BudgetLimit? limit = _limits.LastOrDefault(x => x.Month <= month);
        return limit?.Amount;
    }

    /// <summary>
    /// Whether <paramref name="transaction"/> counts towards it: on one of its accounts, or any when it has none, and
    /// carrying one of its tags, or any spending when it has none. Transfers never count, see
    /// <see cref="BankTransaction.CountsAsIncomeOrSpending"/>.
    /// </summary>
    /// <param name="transaction">The transaction to check.</param>
    public bool Covers(BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return Covers(transaction, transaction.AccountId);
    }

    /// <summary>
    /// <see cref="Covers(BankTransaction)"/>, with <paramref name="transaction"/> counting as on
    /// <paramref name="countsOnAccountId"/>. A merged joint account's shown copy, when it's on the hidden copy and fills
    /// a gap in the shown one, see <see cref="Sharing.JointAccount"/>.
    /// </summary>
    /// <param name="transaction">The transaction to check.</param>
    /// <param name="countsOnAccountId">The account it counts as being on.</param>
    public bool Covers(BankTransaction transaction, Guid countsOnAccountId)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (CoversNothing || !transaction.CountsAsIncomeOrSpending)
        {
            return false;
        }

        if (_accountIds.Count > 0 && !_accountIds.Contains(countsOnAccountId))
        {
            return false;
        }

        if (_tagIds.Count == 0)
        {
            return true;
        }

        return transaction.CustomClassificationIds.Any(_tagIds.Contains);
    }

    /// <summary>
    /// How much of <paramref name="transaction"/> counts towards it, negative for money out. With tags, the parts
    /// carrying them when it's split, see <see cref="BankTransaction.AmountUnder"/>. Without, all of it, but money in
    /// only when it's a refund, so salary paid into a joint account doesn't cancel what was spent from it.
    /// </summary>
    /// <param name="transaction">The transaction to check.</param>
    public decimal AmountOf(BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return AmountOf(transaction, transaction.AccountId);
    }

    /// <summary>
    /// <see cref="AmountOf(BankTransaction)"/>, with <paramref name="transaction"/> counting as on
    /// <paramref name="countsOnAccountId"/>, see <see cref="Covers(BankTransaction, Guid)"/>.
    /// </summary>
    /// <param name="transaction">The transaction to check.</param>
    /// <param name="countsOnAccountId">The account it counts as being on.</param>
    public decimal AmountOf(BankTransaction transaction, Guid countsOnAccountId)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!Covers(transaction, countsOnAccountId))
        {
            return 0;
        }

        if (_tagIds.Count > 0)
        {
            return transaction.AmountUnder(_tagIds);
        }

        if (transaction.Amount < 0 || transaction.IsRefund)
        {
            return transaction.Amount;
        }

        return 0;
    }
}
