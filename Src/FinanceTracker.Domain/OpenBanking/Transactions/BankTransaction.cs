using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain.OpenBanking.Transactions;

/// <summary>
/// A transaction on a <see cref="BankAccount"/>. Its own aggregate as the set is unbounded.
/// </summary>
public sealed class BankTransaction : AggregateRoot
{
    private readonly List<string> _providerClassifications = [];
    private readonly List<Guid> _customClassificationIds = [];

    [Obsolete("EF Use Only")]
    private BankTransaction()
    {
    }

    private BankTransaction(Guid userId, Guid accountId, TransactionDetails details) : base(userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details.ExternalId);

        AccountId = accountId;
        ExternalId = details.ExternalId;
        UpdateFromSync(details);
    }

    public Guid AccountId { get; private set; }

    [Encrypt] public string ExternalId { get; private set; }

    [Encrypt] public string Description { get; private set; }

    [Encrypt] public TransactionType Type { get; private set; }

    [Encrypt] public string Category { get; private set; }

    [Encrypt] public decimal Amount { get; private set; }

    [Encrypt] public string Currency { get; private set; }

    [Encrypt] public DateTime OccurredAt { get; private set; }

    public bool IsPending { get; private set; }

    /// <summary>
    /// Tags supplied by the provider.
    /// </summary>
    [Encrypt] public IReadOnlyCollection<string> ProviderClassifications => _providerClassifications.AsReadOnly();

    /// <summary>
    /// User applied <see cref="CustomClassification"/>s.
    /// </summary>
    public IReadOnlyCollection<Guid> CustomClassificationIds => _customClassificationIds.AsReadOnly();

    public static BankTransaction Import(BankAccount account, TransactionDetails details)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(details);

        return new BankTransaction(account.UserId, account.Id, details);
    }

    [MemberNotNull(nameof(Description), nameof(Category), nameof(Currency))]
    public void UpdateFromSync(TransactionDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (ExternalId is not null && ExternalId != details.ExternalId)
        {
            throw new InvalidOperationException(
                $"Cannot update transaction {Id} with details for a different transaction.");
        }

        Description = details.Description;
        Type = details.Type;
        Category = details.Category;
        Amount = details.Amount;
        Currency = details.Currency;
        OccurredAt = details.OccurredAt;
        IsPending = details.IsPending;

        _providerClassifications.Clear();
        _providerClassifications.AddRange(details.ProviderClassifications.Distinct());
    }

    public void Classify(CustomClassification classification)
    {
        ArgumentNullException.ThrowIfNull(classification);
        if (classification.UserId != UserId)
        {
            throw new InvalidOperationException("Cannot apply another user's classification to this transaction.");
        }

        if (!_customClassificationIds.Contains(classification.Id))
        {
            _customClassificationIds.Add(classification.Id);
        }
    }

    public void Unclassify(Guid customClassificationId) => _customClassificationIds.Remove(customClassificationId);
}
