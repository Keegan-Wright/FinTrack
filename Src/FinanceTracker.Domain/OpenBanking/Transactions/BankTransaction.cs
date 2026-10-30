using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Domain.OpenBanking.Transactions;

/// <summary>
/// A transaction on a <see cref="BankAccount"/>. Its own aggregate as the set is unbounded.
/// </summary>
public sealed class BankTransaction : AggregateRoot
{
    private readonly List<string> _providerClassifications = [];
    private readonly List<Guid> _customClassificationIds = [];
    private readonly List<RuleTag> _ruleTags = [];
    private readonly List<HiddenClassification> _hiddenClassifications = [];

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
    /// Tags supplied by the provider, all of them. Rules look at these, the user sees
    /// <see cref="VisibleProviderClassifications"/>.
    /// </summary>
    public IReadOnlyCollection<string> ProviderClassifications => _providerClassifications.AsReadOnly();

    /// <summary>
    /// The bank's classifications the user or their rules have hidden, e.g. <c>TV</c> on a mortgage payment (Thanks lloyds?).
    /// </summary>
    public IReadOnlyList<HiddenClassification> HiddenClassifications => _hiddenClassifications.AsReadOnly();

    /// <summary>
    /// The bank's classifications less the hidden ones, what's shown, filtered on and reported.
    /// </summary>
    public IEnumerable<string> VisibleProviderClassifications =>
        _providerClassifications.Where(x => FindHidden(x) is null);

    /// <summary>
    /// <see cref="CustomClassification"/>s applied by the user or by rules, see <see cref="RuleTags"/> for which.
    /// </summary>
    public IReadOnlyCollection<Guid> CustomClassificationIds => _customClassificationIds.AsReadOnly();

    /// <summary>
    /// The <see cref="CustomClassificationIds"/> rules added, with the rule that added each. The rest the user added.
    /// </summary>
    public IReadOnlyCollection<RuleTag> RuleTags => _ruleTags.AsReadOnly();

    /// <summary>
    /// Whether the user has added or removed a tag by hand, after which rules leave its tags alone so a tag the user
    /// removed is never added back.
    /// </summary>
    public bool TagsSetByUser { get; private set; }

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

    /// <summary>
    /// The user tags the transaction, which from now on rules leave its tags alone, see <see cref="TagsSetByUser"/>.
    /// </summary>
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

        _ruleTags.RemoveAll(x => x.ClassificationId == classification.Id);
        TagsSetByUser = true;
    }

    /// <summary>
    /// The user removes a tag, which from now on rules leave its tags alone, see <see cref="TagsSetByUser"/>.
    /// </summary>
    public void Unclassify(Guid customClassificationId)
    {
        RemoveClassification(customClassificationId);
        TagsSetByUser = true;
    }

    /// <summary>
    /// Removes a tag that's been deleted. Not the user editing this transaction, so rules still apply to it.
    /// </summary>
    public void RemoveDeletedClassification(Guid customClassificationId) =>
        RemoveClassification(customClassificationId);

    /// <summary>
    /// Whether rule <paramref name="ruleId"/> added one of its tags or gave its transfer answer, so re-applying the
    /// rules here can take back what that rule no longer gives.
    /// </summary>
    public bool WasDecidedBy(Guid ruleId) =>
        _ruleTags.Exists(x => x.RuleId == ruleId) || Transfer?.RuleId == ruleId ||
        _hiddenClassifications.Exists(x => x.RuleId == ruleId);

    /// <summary>
    /// Hides one of the bank's classifications by hand. A rule hiding it too no longer counts, the user's stands.
    /// </summary>
    /// <param name="classification">One of <see cref="ProviderClassifications"/>, in any case.</param>
    /// <returns>Whether it wasn't already hidden by the user.</returns>
    /// <exception cref="ArgumentException">The bank didn't send <paramref name="classification"/>.</exception>
    public bool HideClassification(string classification)
    {
        string bankClassification = BankClassification(classification)
            ?? throw new ArgumentException($"The bank didn't classify this as '{classification}'.",
                nameof(classification));

        HiddenClassification? hidden = FindHidden(bankClassification);
        if (hidden is { HiddenByUser: true })
        {
            return false;
        }

        if (hidden is not null)
        {
            _hiddenClassifications.Remove(hidden);
        }

        _hiddenClassifications.Add(new HiddenClassification(bankClassification, null));
        return true;
    }

    /// <summary>
    /// Shows a bank classification the user hid. One a rule hid stays hidden until the rule changes.
    /// </summary>
    /// <returns>Whether the user had hidden it.</returns>
    public bool ShowClassification(string classification)
    {
        HiddenClassification? hidden = FindHidden(classification);
        if (hidden is not { HiddenByUser: true })
        {
            return false;
        }

        _hiddenClassifications.Remove(hidden);
        return true;
    }

    /// <summary>
    /// Records what the user's rules decided, from <see cref="RuleEvaluator.Evaluate"/>. Tags and a transfer answer a
    /// rule gave before but no longer does are removed, so editing or disabling a rule undoes what it did. Tags are
    /// left alone once <see cref="TagsSetByUser"/>, and the user's transfer decision is never replaced.
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    public bool ApplyRules(RuleOutcome outcome, DateTime decidedAt)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        bool tagsChanged = ApplyRuleTags(outcome.Tags);
        bool transferChanged = ApplyRuleTransfer(outcome.Transfer, decidedAt);
        bool hiddenChanged = ApplyRuleHiding(outcome.HiddenClassifications);

        return tagsChanged || transferChanged || hiddenChanged;
    }

    /// <summary>
    /// Hides what the rules hide that this transaction has, and shows again what they no longer hide. The user's own
    /// hiding is left as it is.
    /// </summary>
    private bool ApplyRuleHiding(IReadOnlyList<HiddenClassification> hidden)
    {
        List<HiddenClassification> wanted = [];
        foreach (HiddenClassification rule in hidden)
        {
            string? bankClassification = BankClassification(rule.Classification);
            if (bankClassification is not null && FindHidden(bankClassification) is not { HiddenByUser: true })
            {
                wanted.Add(rule with { Classification = bankClassification });
            }
        }

        List<HiddenClassification> current = _hiddenClassifications.Where(static x => !x.HiddenByUser).ToList();
        if (current.ToHashSet().SetEquals(wanted))
        {
            return false;
        }

        _hiddenClassifications.RemoveAll(static x => !x.HiddenByUser);
        _hiddenClassifications.AddRange(wanted);
        return true;
    }

    /// <summary>
    /// The bank's own spelling of <paramref name="classification"/>, null when it didn't send it.
    /// </summary>
    private string? BankClassification(string classification) => _providerClassifications.Find(x =>
        string.Equals(x, classification, StringComparison.OrdinalIgnoreCase));

    private HiddenClassification? FindHidden(string classification) => _hiddenClassifications.Find(x =>
        string.Equals(x.Classification, classification, StringComparison.OrdinalIgnoreCase));

    private bool ApplyRuleTags(IReadOnlyList<RuleTag> tags)
    {
        if (TagsSetByUser || _ruleTags.ToHashSet().SetEquals(tags))
        {
            return false;
        }

        foreach (RuleTag previous in _ruleTags)
        {
            _customClassificationIds.Remove(previous.ClassificationId);
        }

        _ruleTags.Clear();

        foreach (RuleTag tag in tags)
        {
            if (!_customClassificationIds.Contains(tag.ClassificationId))
            {
                _customClassificationIds.Add(tag.ClassificationId);
            }

            _ruleTags.Add(tag);
        }

        return true;
    }

    private bool ApplyRuleTransfer(RuleTransfer? transfer, DateTime decidedAt)
    {
        if (transfer is null)
        {
            // Only a rule's own answer is taken back, the matcher's and the user's stay
            if (Transfer?.Source != DecisionSource.Rule)
            {
                return false;
            }

            Transfer = null;
            return true;
        }

        TransferInfo decision = TransferInfo.FromRule(transfer.Decision, transfer.RuleId, decidedAt);
        if (decision.IsSameAs(Transfer))
        {
            return false;
        }

        return DecideTransfer(decision);
    }

    private void RemoveClassification(Guid customClassificationId)
    {
        _customClassificationIds.Remove(customClassificationId);
        _ruleTags.RemoveAll(x => x.ClassificationId == customClassificationId);
    }
}
