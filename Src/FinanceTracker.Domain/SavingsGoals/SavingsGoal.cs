using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.SavingsGoals;

/// <summary>
/// Something the user is saving up for, e.g. £10,000 for a car by next summer.
/// </summary>
/// <remarks>
/// What's saved comes from its <see cref="Source"/> typed in, a savings account's balance, or tagged transactions.
/// <see cref="SavingsGoalCalculator"/> works it out for the last two.
/// </remarks>
public sealed class SavingsGoal : AggregateRoot
{
    private readonly List<Guid> _tagIds = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private SavingsGoal()
    {
    }
#pragma warning restore CS8618

    /// <summary>
    /// A goal whose saved amount the user types in, see <see cref="TrackAccount"/> and <see cref="TrackTags"/> for the
    /// others.
    /// </summary>
    /// <param name="userId">The user it belongs to.</param>
    /// <param name="name">What the user calls it.</param>
    /// <param name="saved">What's saved so far.</param>
    /// <param name="monthlyContribution">How much the user puts towards it each month.</param>
    /// <param name="target">How much they're saving up to, 0 for no target.</param>
    /// <param name="targetDate">When they want to reach it by, null for no date.</param>
    public SavingsGoal(Guid userId, string name, decimal saved, decimal monthlyContribution, decimal target,
        DateTime? targetDate) : base(userId)
    {
        Rename(name);
        TrackManually(saved);
        ChangeMonthlyContribution(monthlyContribution);
        SetTarget(target, targetDate);
    }

    /// <summary>
    /// What the user calls it.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Where what's saved comes from.
    /// </summary>
    public SavingsGoalSource Source { get; private set; }

    /// <summary>
    /// What the user typed in for <see cref="SavingsGoalSource.Manual"/>, or what was saved before
    /// <see cref="TrackedFrom"/> for <see cref="SavingsGoalSource.Tags"/>. Unused for an account.
    /// </summary>
    public decimal Saved { get; private set; }

    /// <summary>
    /// The savings account backing it for <see cref="SavingsGoalSource.Account"/>, null otherwise.
    /// </summary>
    public Guid? AccountId { get; private set; }

    /// <summary>
    /// The tags whose transactions count towards it for <see cref="SavingsGoalSource.Tags"/>, empty otherwise.
    /// </summary>
    public IReadOnlyCollection<Guid> TagIds => _tagIds.AsReadOnly();

    /// <summary>
    /// The first day tagged transactions count for <see cref="SavingsGoalSource.Tags"/>, null otherwise.
    /// </summary>
    public DateOnly? TrackedFrom { get; private set; }

    /// <summary>
    /// Which way tagged money moves when it's saved, for <see cref="SavingsGoalSource.Tags"/>. Money in for any other.
    /// </summary>
    public SavingsDirection Direction { get; private set; }

    /// <summary>
    /// How much the user puts towards it each month.
    /// </summary>
    public decimal MonthlyContribution { get; private set; }

    /// <summary>
    /// How much the user is saving up to, 0 for no target.
    /// </summary>
    public decimal Target { get; private set; }

    /// <summary>
    /// When the user wants to reach <see cref="Target"/> by.
    /// </summary>
    public DateTime? TargetDate { get; private set; }

    /// <summary>
    /// Whether it tracks tags and all of them were deleted, so nothing counts until it's given one.
    /// </summary>
    public bool CoversNothing => Source == SavingsGoalSource.Tags && _tagIds.Count == 0;

    /// <summary>
    /// Whether <paramref name="transaction"/> moves what's saved: any on the goal's account, or one with a goal's tag
    /// from its <see cref="TrackedFrom"/> day on. Nothing counts towards a typed in goal.
    /// </summary>
    /// <param name="transaction">The transaction to check.</param>
    /// <param name="timeZone">Where the user is, so a transaction falls on the day it was where they are.</param>
    public bool Counts(BankTransaction transaction, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(timeZone);

        if (Source == SavingsGoalSource.Account)
        {
            return AccountId == transaction.AccountId;
        }

        if (Source != SavingsGoalSource.Tags || TrackedFrom is not { } from)
        {
            return false;
        }

        if (!transaction.CustomClassificationIds.Any(_tagIds.Contains))
        {
            return false;
        }

        DateTime local = TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(transaction.OccurredAt, DateTimeKind.Utc),
            timeZone);
        return DateOnly.FromDateTime(local) >= from;
    }

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
    /// Adds to a typed in amount.
    /// </summary>
    /// <param name="amount">How much to add, 0 or more.</param>
    public void AddSavings(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Saved += amount;
    }

    /// <summary>
    /// Saved is what the user types in, starting at <paramref name="saved"/>.
    /// </summary>
    /// <param name="saved">What's saved so far.</param>
    public void TrackManually(decimal saved)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(saved);
        Source = SavingsGoalSource.Manual;
        Saved = saved;
        AccountId = null;
        _tagIds.Clear();
        TrackedFrom = null;
        Direction = SavingsDirection.MoneyIn;
    }

    /// <summary>
    /// Saved is <paramref name="accountId"/>'s current balance.
    /// </summary>
    /// <param name="accountId">The savings account.</param>
    public void TrackAccount(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("A goal needs an account to track.", nameof(accountId));
        }

        Source = SavingsGoalSource.Account;
        Saved = 0;
        AccountId = accountId;
        _tagIds.Clear();
        TrackedFrom = null;
        Direction = SavingsDirection.MoneyIn;
    }

    /// <summary>
    /// Saved is <paramref name="startingAmount"/> plus the money in, less the money out, of transactions with any of
    /// <paramref name="tagIds"/> from <paramref name="from"/> on.
    /// </summary>
    /// <exception cref="ArgumentException">There are no tags.</exception>
    /// <param name="tagIds">The tags whose transactions count.</param>
    /// <param name="startingAmount">What was saved before <paramref name="from"/>.</param>
    /// <param name="from">The first day tagged transactions count.</param>
    /// <param name="direction">Which way tagged money moves when it's saved, money in unless it goes somewhere
    /// FinTrack can't see.</param>
    public void TrackTags(IEnumerable<Guid> tagIds, decimal startingAmount, DateOnly from,
        SavingsDirection direction = SavingsDirection.MoneyIn)
    {
        ArgumentNullException.ThrowIfNull(tagIds);
        ArgumentOutOfRangeException.ThrowIfNegative(startingAmount);

        List<Guid> replacements = tagIds.Distinct().ToList();
        if (replacements.Count == 0)
        {
            throw new ArgumentException("A goal needs at least one tag to track.", nameof(tagIds));
        }

        Source = SavingsGoalSource.Tags;
        Saved = startingAmount;
        AccountId = null;
        _tagIds.Clear();
        _tagIds.AddRange(replacements);
        TrackedFrom = from;
        Direction = direction;
    }

    /// <summary>
    /// Drops a tag being deleted. A goal left with none is kept, see <see cref="CoversNothing"/>.
    /// </summary>
    /// <param name="tagId">The tag being deleted.</param>
    /// <returns>Whether the goal tracked the tag.</returns>
    public bool RemoveTag(Guid tagId) => _tagIds.Remove(tagId);

    /// <summary>
    /// Changes how much the user puts towards it each month.
    /// </summary>
    /// <param name="monthlyContribution">The amount, 0 or more.</param>
    public void ChangeMonthlyContribution(decimal monthlyContribution)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyContribution);
        MonthlyContribution = monthlyContribution;
    }

    /// <summary>
    /// Changes what the user is saving up to and by when.
    /// </summary>
    /// <param name="target">The amount, 0 for no target.</param>
    /// <param name="targetDate">When to reach it by, null for no date.</param>
    public void SetTarget(decimal target, DateTime? targetDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(target);
        Target = target;
        TargetDate = targetDate;
    }

    /// <summary>
    /// Someone the goal is shared with, allowed to change it, can change its name, target and contributions, but what
    /// it tracks stays its owner's to choose. Nothing to check for its owner.
    /// </summary>
    /// <param name="by">Who's changing it.</param>
    /// <param name="source">Where the change would have what's saved come from.</param>
    /// <param name="accountId">The account the change would track.</param>
    /// <param name="tagIds">The tags the change would track.</param>
    /// <param name="trackedFrom">The day the change would count tagged transactions from.</param>
    /// <param name="direction">Which way the change would have tagged money move when it's saved.</param>
    /// <exception cref="DomainRuleException">Someone other than its owner would have it track something else.
    /// </exception>
    public void EnsureTrackingIsUnchangedFor(Guid by, SavingsGoalSource source, Guid? accountId,
        IReadOnlyCollection<Guid> tagIds, DateOnly? trackedFrom, SavingsDirection direction)
    {
        ArgumentNullException.ThrowIfNull(tagIds);
        if (by == UserId || TracksTheSame(source, accountId, tagIds, trackedFrom, direction))
        {
            return;
        }

        throw new DomainRuleException("Only its owner can change what a shared goal tracks.");
    }

    /// <summary>
    /// Whether tracking this would track what the goal tracks now. Only what its source uses counts, e.g. the tags
    /// don't for a goal tracking an account.
    /// </summary>
    /// <param name="source">Where what's saved would come from.</param>
    /// <param name="accountId">The account it would track.</param>
    /// <param name="tagIds">The tags it would track.</param>
    /// <param name="trackedFrom">The day it would count tagged transactions from.</param>
    /// <param name="direction">Which way tagged money would move when it's saved.</param>
    private bool TracksTheSame(SavingsGoalSource source, Guid? accountId, IReadOnlyCollection<Guid> tagIds,
        DateOnly? trackedFrom, SavingsDirection direction)
    {
        if (source != Source)
        {
            return false;
        }

        if (Source == SavingsGoalSource.Account)
        {
            return accountId == AccountId;
        }

        if (Source == SavingsGoalSource.Tags)
        {
            return tagIds.ToHashSet().SetEquals(_tagIds) && trackedFrom == TrackedFrom && direction == Direction;
        }

        return true;
    }
}
