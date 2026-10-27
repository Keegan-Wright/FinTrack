using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Domain.Household;

/// <summary>
/// User defined house hold members
/// A house hold member might pull more than their own finances into the app, this allowes other incomes to be factored in.
/// </summary>
public sealed class HouseholdMember : AggregateRoot
{
    private readonly List<AccountIdentifier> _linkedAccounts = [];
    private readonly List<string> _payeeNames = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private HouseholdMember()
    {
    }
#pragma warning restore CS8618

    public HouseholdMember(Guid userId, string firstName, string lastName, decimal? income) : base(userId)
    {
        Rename(firstName, lastName);
        ChangeIncome(income);
    }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public decimal? Income { get; private set; }

    /// <summary>
    /// The member's own accounts, which aren't connected to the app. Money to or from them is a household transfer.
    /// </summary>
    public IReadOnlyList<AccountIdentifier> LinkedAccounts => _linkedAccounts.AsReadOnly();

    /// <summary>
    /// How the member appears in transaction descriptions, e.g. <c>J Smith</c>. For banks that name the payer or
    /// payee but not their account.
    /// </summary>
    public IReadOnlyList<string> PayeeNames => _payeeNames.AsReadOnly();

    [MemberNotNull(nameof(FirstName), nameof(LastName))]
    public void Rename(string firstName, string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        FirstName = firstName;
        LastName = lastName;
    }

    public void ChangeIncome(decimal? income)
    {
        if (income.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(income.Value, nameof(income));
        }

        Income = income;
    }

    /// <summary>
    /// Links one of the member's accounts, ignored when it's already linked.
    /// </summary>
    public void LinkAccount(AccountIdentifier account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (!_linkedAccounts.Contains(account))
        {
            _linkedAccounts.Add(account);
        }
    }

    /// <summary>
    /// Adds a name the member is paid or pays as, ignored when it's already there in any case.
    /// </summary>
    public void AddPayeeName(string payeeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payeeName);

        string trimmed = payeeName.Trim();
        if (!_payeeNames.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            _payeeNames.Add(trimmed);
        }
    }

    /// <summary>
    /// Replaces how the member is recognised with <paramref name="payeeNames"/> and
    /// <paramref name="linkedAccounts"/>, duplicates dropped as <see cref="AddPayeeName"/> and
    /// <see cref="LinkAccount"/> do. Nothing changes when any of them is invalid.
    /// </summary>
    public void ReplaceRecognition(IEnumerable<string> payeeNames, IEnumerable<AccountIdentifier> linkedAccounts)
    {
        ArgumentNullException.ThrowIfNull(payeeNames);
        ArgumentNullException.ThrowIfNull(linkedAccounts);

        List<string> names = payeeNames.ToList();
        List<AccountIdentifier> accounts = linkedAccounts.ToList();
        foreach (string name in names)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(payeeNames));
        }

        foreach (AccountIdentifier account in accounts)
        {
            ArgumentNullException.ThrowIfNull(account, nameof(linkedAccounts));
        }

        _payeeNames.Clear();
        _linkedAccounts.Clear();
        names.ForEach(AddPayeeName);
        accounts.ForEach(LinkAccount);
    }

    /// <summary>
    /// Whether a transaction is money to or from this member, its reference names one of their linked accounts or
    /// its description starts with one of their payee names.
    /// </summary>
    /// <remarks>
    /// Starts with rather than equals, as banks add to the name, e.g. <c>J SMITH 01JAN26</c>, and ignoring
    /// case as banks often use capitals. The name has to end at a word boundary, so <c>J Smith</c> doesn't recognise
    /// <c>J SMITHSON LTD</c>.
    /// </remarks>
    public bool Recognises(string? description, string? reference) =>
        _linkedAccounts.Any(x => x.IsNamedIn(reference)) ||
        _payeeNames.Any(x => StartsWithName(description, x));

    private static bool StartsWithName(string? description, string payeeName)
    {
        ReadOnlySpan<char> text = description.AsSpan().TrimStart();
        return text.StartsWith(payeeName, StringComparison.OrdinalIgnoreCase) &&
               (text.Length == payeeName.Length || !char.IsLetterOrDigit(text[payeeName.Length]));
    }
}
