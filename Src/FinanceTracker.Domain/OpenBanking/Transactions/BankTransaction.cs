using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Domain.OpenBanking.Transactions;

/// <summary>
/// A transaction on a <see cref="BankAccount"/>. Its own aggregate as the set is unbounded.
/// </summary>
public sealed class BankTransaction : AggregateRoot
{
    private readonly List<string> _providerClassifications = [];
    private readonly List<Guid> _customClassificationIds = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private BankTransaction()
    {
    }
#pragma warning restore CS8618

    private BankTransaction(Guid userId, Guid accountId, TransactionDetails details) : base(userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details.ExternalId);

        AccountId = accountId;
        ExternalId = details.ExternalId;
        UpdateFromSync(details);
    }

    public Guid AccountId { get; private set; }

    public string ExternalId { get; private set; }

    public string Description { get; private set; }

    public TransactionType Type { get; private set; }

    public string Category { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public bool IsPending { get; private set; }

    /// <summary>
    /// Whether the provider labels it a transfer. Only a hint, many aren't between the user's own accounts.
    /// </summary>
    public bool LabelledAsTransfer { get; private set; }

    /// <summary>
    /// The provider's raw details of who the transaction was with.
    /// </summary>
    public TransactionMetadata Metadata { get; private set; }

    /// <summary>
    /// Whether this is money moved between the user's own accounts, null until something decides.
    /// </summary>
    public TransferInfo? Transfer { get; private set; }

    /// <summary>
    /// Whether the transaction belongs in income and spending totals, every transaction does unless it's decided to
    /// be a transfer.
    /// </summary>
    public bool CountsAsIncomeOrSpending => Transfer?.Decision is not TransferDecision.Transfer;

    /// <summary>
    /// Tags supplied by the provider.
    /// </summary>
    public IReadOnlyCollection<string> ProviderClassifications => _providerClassifications.AsReadOnly();

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

    [MemberNotNull(nameof(Description), nameof(Category), nameof(Currency), nameof(Metadata))]
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
        Metadata = details.Metadata;
        LabelledAsTransfer = details.LabelledAsTransfer;

        _providerClassifications.Clear();
        _providerClassifications.AddRange(details.ProviderClassifications.Distinct());
    }

    /// <summary>
    /// Records <paramref name="transfer"/> unless a source with higher precedence has already decided.
    /// </summary>
    /// <returns>Whether it was recorded.</returns>
    public bool DecideTransfer(TransferInfo transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (transfer.PartnerTransactionId == Id)
        {
            throw new InvalidOperationException($"Transaction {Id} cannot be its own transfer partner.");
        }

        if (Transfer is not null && Transfer.Source > transfer.Source)
        {
            return false;
        }

        Transfer = transfer;
        return true;
    }

    /// <summary>
    /// Removes the decision if <paramref name="source"/> or a lower one made it, so the transaction counts again.
    /// </summary>
    /// <returns>Whether there was a decision to remove.</returns>
    public bool ClearTransfer(DecisionSource source)
    {
        if (Transfer is null || Transfer.Source > source)
        {
            return false;
        }

        Transfer = null;
        return true;
    }

    /// <summary>
    /// The user says this is a transfer, keeping any partner already found e.g. when confirming a match.
    /// </summary>
    public void MarkAsTransfer(DateTime decidedAt) =>
        Transfer = new TransferInfo(TransferDecision.Transfer, DecisionSource.User, Transfer?.Reason,
            Transfer?.PartnerTransactionId, Transfer?.PartnerAccountId, Transfer?.PartnerHouseholdMemberId, decidedAt);

    /// <summary>
    /// The user says this counts as income or spending, which the matcher then leaves alone.
    /// </summary>
    public void MarkNotATransfer(DateTime decidedAt) =>
        Transfer = new TransferInfo(TransferDecision.NotATransfer, DecisionSource.User, null, null, null, null,
            decidedAt);

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
