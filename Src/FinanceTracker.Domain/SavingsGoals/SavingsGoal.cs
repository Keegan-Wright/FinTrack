using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

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
    public SavingsGoal(Guid userId, string name, decimal saved, decimal monthlyContribution, decimal target,
        DateTime? targetDate) : base(userId)
    {
        Rename(name);
        TrackManually(saved);
        ChangeMonthlyContribution(monthlyContribution);
        SetTarget(target, targetDate);
    }

    public string Name { get; private set; }

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

    [MemberNotNull(nameof(Name))]
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// Adds to a typed in amount.
    /// </summary>
    public void AddSavings(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Saved += amount;
    }

    /// <summary>
    /// Saved is what the user types in, starting at <paramref name="saved"/>.
    /// </summary>
    public void TrackManually(decimal saved)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(saved);
        Source = SavingsGoalSource.Manual;
        Saved = saved;
        AccountId = null;
        _tagIds.Clear();
        TrackedFrom = null;
    }

    /// <summary>
    /// Saved is <paramref name="accountId"/>'s current balance.
    /// </summary>
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
    }

    /// <summary>
    /// Saved is <paramref name="startingAmount"/> plus the money in, less the money out, of transactions with any of
    /// <paramref name="tagIds"/> from <paramref name="from"/> on.
    /// </summary>
    /// <exception cref="ArgumentException">There are no tags.</exception>
    public void TrackTags(IEnumerable<Guid> tagIds, decimal startingAmount, DateOnly from)
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
    }

    /// <summary>
    /// Drops a tag being deleted. A goal left with none is kept, see <see cref="CoversNothing"/>.
    /// </summary>
    /// <returns>Whether the goal tracked the tag.</returns>
    public bool RemoveTag(Guid tagId) => _tagIds.Remove(tagId);

    public void ChangeMonthlyContribution(decimal monthlyContribution)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyContribution);
        MonthlyContribution = monthlyContribution;
    }

    public void SetTarget(decimal target, DateTime? targetDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(target);
        Target = target;
        TargetDate = targetDate;
    }
}
