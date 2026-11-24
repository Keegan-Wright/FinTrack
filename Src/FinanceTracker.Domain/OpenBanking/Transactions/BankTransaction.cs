using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Domain.OpenBanking.Transactions;

/// <summary>
/// A transaction on a <see cref="BankAccount"/>. Its own aggregate as the set is unbounded.
/// </summary>
public sealed class BankTransaction : AggregateRoot, IBelongsToAccount
{
    /// <summary>
    /// The bank's category for a card purchase, see <see cref="IsRefund"/>.
    /// </summary>
    private const string PurchaseCategory = "PURCHASE";

    private readonly List<string> _providerClassifications = [];
    private readonly List<Guid> _customClassificationIds = [];
    private readonly List<RuleTag> _ruleTags = [];
    private readonly List<HiddenClassification> _hiddenClassifications = [];
    private readonly List<TransactionSplit> _splits = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private BankTransaction()
    {
    }
#pragma warning restore CS8618

    /// <param name="userId">The user it belongs to.</param>
    /// <param name="accountId">The account it's on.</param>
    /// <param name="details">What the provider reports for it.</param>
    private BankTransaction(Guid userId, Guid accountId, TransactionDetails details) : base(userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details.ExternalId);

        AccountId = accountId;
        ExternalId = details.ExternalId;
        UpdateFromSync(details);
    }

    /// <inheritdoc/>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// The provider's id for it.
    /// </summary>
    public string ExternalId { get; private set; }

    /// <summary>
    /// The bank's description, usually who it was with.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Whether money went out or came in.
    /// </summary>
    public TransactionType Type { get; private set; }

    /// <summary>
    /// The provider's category for it, e.g. a purchase or a transfer.
    /// </summary>
    public string Category { get; private set; }

    /// <summary>
    /// How much it was, negative for money going out.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// Its currency code, e.g. GBP.
    /// </summary>
    public string Currency { get; private set; }

    /// <summary>
    /// When it happened, in UTC.
    /// </summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>
    /// Whether the bank hasn't settled it yet.
    /// </summary>
    public bool IsPending { get; private set; }

    /// <summary>
    /// Whether it's on the hidden copy of a joint account and stands in for the shown copy, in a stretch the shown copy
    /// missed while its connection lapsed, see <see cref="Sharing.JointAccount.Gaps"/>.
    /// </summary>
    public bool FillsJointGap { get; private set; }

    /// <summary>
    /// Sets whether it fills a joint account's gap, see <see cref="FillsJointGap"/>.
    /// </summary>
    /// <param name="fills">Whether it fills the gap.</param>
    /// <returns>Whether that changed.</returns>
    public bool MarkFillsJointGap(bool fills)
    {
        if (FillsJointGap == fills)
        {
            return false;
        }

        FillsJointGap = fills;
        return true;
    }

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
    /// Whether this pays a <see cref="Bill"/>, null until something decides.
    /// </summary>
    public BillPayment? BillPayment { get; private set; }

    /// <summary>
    /// Whether this pays off a <see cref="Debt"/>, null until a rule or the user decides.
    /// </summary>
    public DebtPayment? DebtPayment { get; private set; }

    /// <summary>
    /// Whether the transaction belongs in income and spending totals, every transaction does unless it's decided to
    /// be a transfer.
    /// </summary>
    public bool CountsAsIncomeOrSpending => Transfer?.Decision is not TransferDecision.Transfer;

    /// <summary>
    /// Whether it's money back on a purchase. Banks send card refunds under the purchase category, while salary and
    /// transfers come in under others.
    /// </summary>
    public bool IsRefund =>
        Amount > 0 && string.Equals(Category, PurchaseCategory, StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// The parts the user split it into, adding up to <see cref="Amount"/>, empty when it isn't split. A split
    /// transaction's <see cref="CustomClassificationIds"/> are its parts' tags.
    /// </summary>
    public IReadOnlyList<TransactionSplit> Splits => _splits.AsReadOnly();

    /// <summary>
    /// Whether the user split it, see <see cref="Splits"/>.
    /// </summary>
    public bool IsSplit => _splits.Count > 0;

    /// <summary>
    /// How much of it counts under any of <paramref name="tagIds"/>, e.g. for a budget: the parts tagged with one of
    /// them when it's split, otherwise all of it when it has one of them, otherwise nothing.
    /// </summary>
    /// <param name="tagIds">The tags to count under.</param>
    public decimal AmountUnder(IReadOnlyCollection<Guid> tagIds)
    {
        ArgumentNullException.ThrowIfNull(tagIds);

        if (IsSplit)
        {
            return _splits.Where(x => x.TagId is { } tagId && tagIds.Contains(tagId)).Sum(static x => x.Amount);
        }

        if (_customClassificationIds.Exists(tagIds.Contains))
        {
            return Amount;
        }

        return 0;
    }

    /// <summary>
    /// A transaction new from a sync on <paramref name="account"/>.
    /// </summary>
    /// <param name="account">The account it's on.</param>
    /// <param name="details">What the provider reports for it.</param>
    public static BankTransaction Import(BankAccount account, TransactionDetails details)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(details);

        return new BankTransaction(account.UserId, account.Id, details);
    }

    /// <summary>
    /// Takes in what a later sync reports for it. A split whose amount changes is undone, its tags stay on the whole.
    /// </summary>
    /// <param name="details">What the provider reports for it now.</param>
    /// <returns>Whether anything differed from what it held, so a sync counts real updates rather than every
    /// transaction the provider sent again.</returns>
    /// <exception cref="InvalidOperationException">The details are for another transaction.</exception>
    [MemberNotNull(nameof(Description), nameof(Category), nameof(Currency), nameof(Metadata))]
    public bool UpdateFromSync(TransactionDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (ExternalId is not null && ExternalId != details.ExternalId)
        {
            throw new InvalidOperationException(
                $"Cannot update transaction {Id} with details for a different transaction.");
        }

        bool changed = !Holds(details);

        // Parts that no longer add up would put the wrong amounts under their tags, the tags stay on the whole
        if (IsSplit && Amount != details.Amount)
        {
            _splits.Clear();
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
        return changed;
    }

    /// <summary>
    /// Whether it already holds everything <paramref name="details"/> reports.
    /// </summary>
    /// <param name="details">What the provider reports for it.</param>
    private bool Holds(TransactionDetails details) =>
        Description == details.Description
        && Type == details.Type
        && Category == details.Category
        && Amount == details.Amount
        && Currency == details.Currency
        && OccurredAt == details.OccurredAt
        && IsPending == details.IsPending
        && Metadata == details.Metadata
        && LabelledAsTransfer == details.LabelledAsTransfer
        && _providerClassifications.SequenceEqual(details.ProviderClassifications.Distinct());

    /// <summary>
    /// Records <paramref name="transfer"/> unless a source with higher precedence has already decided.
    /// </summary>
    /// <param name="transfer">The decision.</param>
    /// <returns>Whether it was recorded.</returns>
    public bool DecideTransfer(TransferInfo transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (transfer.PartnerTransactionId == Id)
        {
            throw new DomainRuleException($"Transaction {Id} cannot be its own transfer partner.");
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
    /// <param name="source">Who's clearing it.</param>
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
    /// <param name="decidedAt">When they said so.</param>
    public void MarkAsTransfer(DateTime decidedAt) =>
        Transfer = new TransferInfo(TransferDecision.Transfer, DecisionSource.User, Transfer?.Reason,
            Transfer?.PartnerTransactionId, Transfer?.PartnerAccountId, Transfer?.PartnerHouseholdMemberId, decidedAt);

    /// <summary>
    /// The user says this counts as income or spending, which the matcher then leaves alone.
    /// </summary>
    /// <param name="decidedAt">When they said so.</param>
    public void MarkNotATransfer(DateTime decidedAt) =>
        Transfer = new TransferInfo(TransferDecision.NotATransfer, DecisionSource.User, null, null, null, null,
            decidedAt);

    /// <summary>
    /// Whether this pays <paramref name="billId"/>.
    /// </summary>
    /// <param name="billId">The bill to check.</param>
    public bool PaysBill(Guid billId) => BillPayment?.BillId == billId;

    /// <summary>
    /// Records <paramref name="payment"/> unless a source with higher precedence has already decided.
    /// </summary>
    /// <param name="payment">The decision.</param>
    /// <returns>Whether it was recorded.</returns>
    public bool DecideBillPayment(BillPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (BillPayment is not null && BillPayment.Source > payment.Source)
        {
            return false;
        }

        BillPayment = payment;
        return true;
    }

    /// <summary>
    /// Removes the decision if <paramref name="source"/> or a lower one made it.
    /// </summary>
    /// <param name="source">Who's clearing it.</param>
    /// <returns>Whether there was a decision to remove.</returns>
    public bool ClearBillPayment(DecisionSource source)
    {
        if (BillPayment is null || BillPayment.Source > source)
        {
            return false;
        }

        BillPayment = null;
        return true;
    }

    /// <summary>
    /// The user says this pays <paramref name="bill"/>, which matching and rules then leave alone.
    /// </summary>
    /// <param name="bill">The bill it pays.</param>
    /// <param name="by">Who says so, the owner or someone they let change it. Only their own bills can be paid, or
    /// one shared with them.</param>
    /// <param name="decidedAt">When they said so.</param>
    /// <param name="sharedWithThem">Whether the bill is shared with <paramref name="by"/>.</param>
    /// <exception cref="InvalidOperationException">The bill is neither <paramref name="by"/>'s nor shared with them.
    /// </exception>
    /// <remarks>On another person's transaction, only <paramref name="by"/>'s own or the owner's, see
    /// <see cref="EnsureEitherOfTheirs"/>.</remarks>
    public void PayBill(Bill bill, Guid by, DateTime decidedAt, bool sharedWithThem = false)
    {
        ArgumentNullException.ThrowIfNull(bill);
        if (bill.UserId != by && !sharedWithThem)
        {
            throw new DomainRuleException("Cannot pay another user's bill with this transaction.");
        }

        EnsureEitherOfTheirs(bill.UserId, by, "bill");

        BillPayment = new BillPayment(bill.Id, DecisionSource.User, decidedAt);
    }

    /// <summary>
    /// On another person's transaction, what <paramref name="by"/> puts on it is theirs or the transaction owner's.
    /// Not a third person's shared with them, which would tell that person of a transaction they can't see, such as
    /// paying their bill, and the owner couldn't name it.
    /// </summary>
    /// <param name="itemOwnerId">Whose the tag, bill or debt is.</param>
    /// <param name="by">Who's putting it on.</param>
    /// <param name="what">What it is, for the message, e.g. <c>bill</c>.</param>
    /// <exception cref="InvalidOperationException">It's a third person's.</exception>
    private void EnsureEitherOfTheirs(Guid itemOwnerId, Guid by, string what)
    {
        if (by == UserId || itemOwnerId == by || itemOwnerId == UserId)
        {
            return;
        }

        throw new DomainRuleException(
            $"Only your own {what} or the transaction owner's can go on another person's transaction.");
    }

    /// <summary>
    /// Drops the payment of a bill being deleted, whoever decided it, so the transaction pays nothing until matched
    /// again.
    /// </summary>
    /// <param name="billId">The bill being deleted.</param>
    /// <returns>Whether it paid the bill.</returns>
    public bool RemoveDeletedBill(Guid billId)
    {
        if (!PaysBill(billId))
        {
            return false;
        }

        BillPayment = null;
        return true;
    }

    /// <summary>
    /// The user says this pays no bill, which matching and rules then leave alone.
    /// </summary>
    /// <param name="decidedAt">When they said so.</param>
    public void PayNoBill(DateTime decidedAt) => BillPayment = new BillPayment(null, DecisionSource.User, decidedAt);

    /// <summary>
    /// Whether this pays off <paramref name="debtId"/>.
    /// </summary>
    /// <param name="debtId">The debt to check.</param>
    public bool PaysDebt(Guid debtId) => DebtPayment?.DebtId == debtId;

    /// <summary>
    /// The user says this pays off <paramref name="debt"/>, which rules then leave alone.
    /// </summary>
    /// <param name="debt">The debt it pays off.</param>
    /// <param name="by">Who says so, the owner or someone they let change it. Only their own debts can be paid, or
    /// one shared with them.</param>
    /// <param name="decidedAt">When they said so.</param>
    /// <param name="sharedWithThem">Whether the debt is shared with <paramref name="by"/>.</param>
    /// <exception cref="InvalidOperationException">The debt is neither <paramref name="by"/>'s nor shared with them.
    /// </exception>
    public void PayDebt(Debt debt, Guid by, DateTime decidedAt, bool sharedWithThem = false)
    {
        ArgumentNullException.ThrowIfNull(debt);
        if (debt.UserId != by && !sharedWithThem)
        {
            throw new DomainRuleException("Cannot pay another user's debt with this transaction.");
        }

        EnsureEitherOfTheirs(debt.UserId, by, "debt");

        DebtPayment = new DebtPayment(debt.Id, DecisionSource.User, decidedAt);
    }

    /// <summary>
    /// The user says this pays off no debt, which rules then leave alone.
    /// </summary>
    /// <param name="decidedAt">When they said so.</param>
    public void PayNoDebt(DateTime decidedAt) => DebtPayment = new DebtPayment(null, DecisionSource.User, decidedAt);

    /// <summary>
    /// Drops the payment of a debt being deleted, whoever decided it.
    /// </summary>
    /// <param name="debtId">The debt being deleted.</param>
    /// <returns>Whether it paid the debt.</returns>
    public bool RemoveDeletedDebt(Guid debtId)
    {
        if (!PaysDebt(debtId))
        {
            return false;
        }

        DebtPayment = null;
        return true;
    }

    /// <summary>
    /// The user tags the transaction, which from now on rules leave its tags alone, see <see cref="TagsSetByUser"/>.
    /// </summary>
    /// <param name="classification">The tag to add.</param>
    /// <param name="by">Who tags it, the owner or someone they let change it. Only their own tags can be used, or
    /// those shared with them.</param>
    /// <param name="sharedWithThem">The tags another person has shared with <paramref name="by"/>.</param>
    /// <exception cref="InvalidOperationException">The tag is neither <paramref name="by"/>'s nor shared with them,
    /// or the transaction is split and its tags are its parts.</exception>
    public void Classify(CustomClassification classification, Guid by,
        IReadOnlyCollection<Guid>? sharedWithThem = null)
    {
        ArgumentNullException.ThrowIfNull(classification);
        if (classification.UserId != by && sharedWithThem?.Contains(classification.Id) != true)
        {
            throw new DomainRuleException("Cannot apply another user's classification to this transaction.");
        }

        EnsureEitherOfTheirs(classification.UserId, by, "tag");

        if (IsSplit)
        {
            throw new DomainRuleException($"Transaction {Id} is split, tag its parts instead.");
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
    /// <param name="customClassificationId">The tag to remove.</param>
    /// <exception cref="InvalidOperationException">The transaction is split and its tags are its parts.</exception>
    public void Unclassify(Guid customClassificationId)
    {
        if (IsSplit)
        {
            throw new DomainRuleException($"Transaction {Id} is split, tag its parts instead.");
        }

        RemoveClassification(customClassificationId);
        TagsSetByUser = true;
    }

    /// <summary>
    /// Removes a tag that's been deleted, leaving the parts it was on untagged. Not the user editing this transaction,
    /// so rules still apply to it.
    /// </summary>
    /// <param name="customClassificationId">The tag being deleted.</param>
    public void RemoveDeletedClassification(Guid customClassificationId)
    {
        RemoveClassification(customClassificationId);

        for (int i = 0; i < _splits.Count; i++)
        {
            if (_splits[i].TagId == customClassificationId)
            {
                _splits[i] = _splits[i] with { TagId = null };
            }
        }
    }

    /// <summary>
    /// The user splits the transaction into <paramref name="parts"/>, replacing any split before. Its tags become the
    /// parts tags and, as when tagging by hand, rules leave them alone from now on.
    /// </summary>
    /// <param name="parts">At least two, each signed like the transaction, adding up to its amount.</param>
    /// <param name="tags">The tags the parts count under.</param>
    /// <param name="by">Who splits it, the owner or someone they let change it. Parts can use their own tags, those
    /// shared with them, or a tag already on the transaction, which may be the other person's.</param>
    /// <param name="sharedWithThem">The tags another person has shared with <paramref name="by"/>.</param>
    /// <exception cref="InvalidOperationException">It's pending, so its amount may still change, or a tag is neither
    /// <paramref name="by"/>'s nor already on it.</exception>
    /// <exception cref="ArgumentException">The parts don't add up, are signed the other way or are zero, there's
    /// only one, or a part's tag isn't among <paramref name="tags"/>.</exception>
    public void Split(IReadOnlyList<TransactionSplit> parts, IReadOnlyCollection<CustomClassification> tags, Guid by,
        IReadOnlyCollection<Guid>? sharedWithThem = null)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(tags);

        if (IsPending)
        {
            throw new DomainRuleException("A pending transaction can't be split until the bank settles it.");
        }

        if (tags.Any(x => x.UserId != by && !HasTag(x.Id) && sharedWithThem?.Contains(x.Id) != true))
        {
            throw new DomainRuleException("Cannot apply another user's classification to this transaction.");
        }

        foreach (CustomClassification tag in tags.Where(x => !HasTag(x.Id)))
        {
            EnsureEitherOfTheirs(tag.UserId, by, "tag");
        }

        CheckParts(parts, tags);

        _splits.Clear();
        _splits.AddRange(parts);

        _customClassificationIds.Clear();
        _customClassificationIds.AddRange(parts.Select(static x => x.TagId).OfType<Guid>().Distinct());
        _ruleTags.Clear();
        TagsSetByUser = true;
    }

    /// <summary>
    /// The user puts the transaction back together. It keeps its parts' tags, on the whole amount.
    /// </summary>
    /// <returns>Whether it was split.</returns>
    public bool Unsplit()
    {
        if (!IsSplit)
        {
            return false;
        }

        _splits.Clear();
        return true;
    }

    /// <summary>
    /// Takes the tags and hidden bank classifications on <paramref name="other"/>, the same transaction as another
    /// person's copy of a joint account has it, adding to its own. With <paramref name="withSplit"/>, its split too
    /// when it isn't split itself. For merging the two copies of a joint account and splitting them again, the tags
    /// may be either person's.
    /// </summary>
    /// <param name="other">The other person's copy of the same transaction.</param>
    /// <param name="withSplit">Whether to take the split, which only the copy being shown decides.</param>
    public void TakeAnnotationsFrom(BankTransaction other, bool withSplit)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (withSplit && !IsSplit && other.IsSplit && other.Amount == Amount)
        {
            _splits.AddRange(other._splits);
            _customClassificationIds.Clear();
        }

        if (IsSplit)
        {
            _customClassificationIds.AddRange(_splits.Select(static x => x.TagId).OfType<Guid>()
                .Except(_customClassificationIds));
        }
        else
        {
            _customClassificationIds.AddRange(other._customClassificationIds.Except(_customClassificationIds));
        }

        foreach (HiddenClassification hidden in other._hiddenClassifications.Where(static x => x.HiddenByUser))
        {
            if (_providerClassifications.Contains(hidden.Classification, StringComparer.OrdinalIgnoreCase) &&
                !_hiddenClassifications.Exists(x => string.Equals(x.Classification, hidden.Classification,
                    StringComparison.OrdinalIgnoreCase)))
            {
                _hiddenClassifications.Add(hidden);
            }
        }

        TagsSetByUser |= other.TagsSetByUser;
    }

    /// <summary>
    /// Whether <paramref name="tagId"/> is on it already, on the whole or on one of its parts.
    /// </summary>
    /// <param name="tagId">The tag to check.</param>
    private bool HasTag(Guid tagId) =>
        _customClassificationIds.Contains(tagId) || _splits.Exists(x => x.TagId == tagId);

    /// <summary>
    /// Checks a split before it's made.
    /// </summary>
    /// <param name="parts">The parts to split it into.</param>
    /// <param name="tags">The tags the parts may have.</param>
    /// <exception cref="ArgumentException">There are fewer than two parts, a part goes the wrong way or is 0, they
    /// don't add up to the amount, or a part's tag doesn't exist.</exception>
    private void CheckParts(IReadOnlyList<TransactionSplit> parts, IReadOnlyCollection<CustomClassification> tags)
    {
        if (parts.Count < 2)
        {
            throw new ArgumentException("Split it into at least two parts.", nameof(parts));
        }

        if (parts.Any(x => x.Amount == 0 || Math.Sign(x.Amount) != Math.Sign(Amount)))
        {
            string direction = Amount < 0 ? "out" : "in";
            throw new ArgumentException($"Each part must be money {direction}, like the transaction.", nameof(parts));
        }

        if (parts.Sum(static x => x.Amount) != Amount)
        {
            throw new ArgumentException("The parts must add up to the transaction's amount.", nameof(parts));
        }

        if (parts.Any(part => part.TagId is { } tagId && !tags.Any(tag => tag.Id == tagId)))
        {
            throw new ArgumentException("One of the parts' tags doesn't exist.", nameof(parts));
        }
    }

    /// <summary>
    /// Whether rule <paramref name="ruleId"/> added one of its tags, gave its transfer answer or said which bill or
    /// debt it pays, so re-applying the rules here can take back what that rule no longer gives.
    /// </summary>
    /// <param name="ruleId">The rule to check.</param>
    public bool WasDecidedBy(Guid ruleId) =>
        _ruleTags.Exists(x => x.RuleId == ruleId) || Transfer?.RuleId == ruleId ||
        _hiddenClassifications.Exists(x => x.RuleId == ruleId) || BillPayment?.RuleId == ruleId ||
        DebtPayment?.RuleId == ruleId;

    /// <summary>
    /// Hides one of the bank's classifications by hand. A rule hiding it too no longer counts, the user's stands.
    /// </summary>
    /// <param name="classification">One of <see cref="ProviderClassifications"/>, in any case.</param>
    /// <returns>Whether it wasn't already hidden by the user.</returns>
    /// <exception cref="ArgumentException">The bank didn't send <paramref name="classification"/>.</exception>
    public bool HideClassification(string classification)
    {
        string bankClassification = BankClassification(classification)
                                    ?? throw new ArgumentException(
                                        $"The bank didn't classify this as '{classification}'.",
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
    /// <param name="classification">The bank classification to show, in any case.</param>
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
    /// Records what the user's rules decided, from <see cref="RuleEvaluator.Evaluate"/>. Tags, a transfer answer, a
    /// bill and a debt a rule gave before but no longer does are removed, so editing or disabling a rule undoes what it
    /// did. Tags are left alone once <see cref="TagsSetByUser"/>, and the user's transfer, bill and debt decisions are
    /// never replaced.
    /// </summary>
    /// <param name="outcome">What the rules decided.</param>
    /// <param name="decidedAt">When they decided.</param>
    /// <returns>Whether anything changed.</returns>
    public bool ApplyRules(RuleOutcome outcome, DateTime decidedAt)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        bool tagsChanged = ApplyRuleTags(outcome.Tags);
        bool transferChanged = ApplyRuleTransfer(outcome.Transfer, decidedAt);
        bool hiddenChanged = ApplyRuleHiding(outcome.HiddenClassifications);
        bool billChanged = ApplyRuleBill(outcome.Bill, decidedAt);
        bool debtChanged = ApplyRuleDebt(outcome.Debt, decidedAt);

        return tagsChanged || transferChanged || hiddenChanged || billChanged || debtChanged;
    }

    /// <summary>
    /// Records the debt the rules say it pays off, or takes back a rule's earlier answer when they no longer say.
    /// </summary>
    /// <param name="debt">The debt the rules say it pays off, null for none.</param>
    /// <param name="decidedAt">When they decided.</param>
    /// <returns>Whether it changed.</returns>
    private bool ApplyRuleDebt(RuleDebt? debt, DateTime decidedAt)
    {
        if (debt is null)
        {
            // Only a rule's own answer is taken back, the user's stays
            if (DebtPayment?.Source != DecisionSource.Rule)
            {
                return false;
            }

            DebtPayment = null;
            return true;
        }

        if (DebtPayment?.Source == DecisionSource.User)
        {
            return false;
        }

        DebtPayment payment = DebtPayment.FromRule(debt.DebtId, debt.RuleId, decidedAt);
        if (payment.IsSameAs(DebtPayment))
        {
            return false;
        }

        DebtPayment = payment;
        return true;
    }

    /// <summary>
    /// Records the bill the rules say it pays, or takes back a rule's earlier answer when they no longer say.
    /// </summary>
    /// <param name="bill">The bill the rules say it pays, null for none.</param>
    /// <param name="decidedAt">When they decided.</param>
    /// <returns>Whether it changed.</returns>
    private bool ApplyRuleBill(RuleBill? bill, DateTime decidedAt)
    {
        if (bill is null)
        {
            // Only a rule's own answer is taken back, the matcher's and the user's stay
            if (BillPayment?.Source != DecisionSource.Rule)
            {
                return false;
            }

            BillPayment = null;
            return true;
        }

        BillPayment payment = BillPayment.FromRule(bill.BillId, bill.RuleId, decidedAt);
        if (payment.IsSameAs(BillPayment))
        {
            return false;
        }

        return DecideBillPayment(payment);
    }

    /// <summary>
    /// Hides what the rules hide that this transaction has, and shows again what they no longer hide. The user's own
    /// hiding is left as it is.
    /// </summary>
    /// <param name="hidden">What the rules hide, each with the rule that hid it.</param>
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
    /// <param name="classification">The classification in any case.</param>
    private string? BankClassification(string classification) => _providerClassifications.Find(x =>
        string.Equals(x, classification, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The hiding of <paramref name="classification"/>, in any case, null when it isn't hidden.
    /// </summary>
    /// <param name="classification">The bank classification.</param>
    private HiddenClassification? FindHidden(string classification) => _hiddenClassifications.Find(x =>
        string.Equals(x.Classification, classification, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Replaces the tags rules added with <paramref name="tags"/>. Left alone once the user has set its tags.
    /// </summary>
    /// <param name="tags">The tags the rules add, each with the rule that added it.</param>
    /// <returns>Whether it changed.</returns>
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

    /// <summary>
    /// Records the rules' transfer answer, or takes back a rule's earlier answer when they no longer give one.
    /// </summary>
    /// <param name="transfer">The rules' transfer answer, null for none.</param>
    /// <param name="decidedAt">When they decided.</param>
    /// <returns>Whether it changed.</returns>
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

    /// <summary>
    /// Takes a tag off, and off the rule tags too.
    /// </summary>
    /// <param name="customClassificationId">The tag.</param>
    private void RemoveClassification(Guid customClassificationId)
    {
        _customClassificationIds.Remove(customClassificationId);
        _ruleTags.RemoveAll(x => x.ClassificationId == customClassificationId);
    }
}
