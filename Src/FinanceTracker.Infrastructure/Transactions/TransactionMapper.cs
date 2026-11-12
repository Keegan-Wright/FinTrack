using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Debts;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Infrastructure.Transactions;

/// <summary>
/// Turns a user's transactions into <see cref="Transaction"/>s, with the names their transfers point at. Accounts,
/// household members and the other half's details. Load it for the transactions being returned, it looks up only
/// what they need.
/// </summary>
internal sealed class TransactionMapper
{
    private readonly IReadOnlyDictionary<Guid, string> _customTags;
    private readonly Dictionary<Guid, string> _accountNames;
    private readonly Dictionary<Guid, string> _householdMemberNames;
    private readonly Dictionary<Guid, BankTransaction> _partners;
    private readonly Dictionary<Guid, string> _ruleNames;
    private readonly Dictionary<Guid, Bill> _bills;
    private readonly Dictionary<Guid, Debt> _debts;
    private readonly List<SavingsGoal> _savingsGoals;
    private readonly TimeZoneInfo _timeZone;
    private readonly AccountsInView _view;

    // Who shares each shared account in view, by account
    private readonly Dictionary<Guid, string> _sharedBy;

    // The names of the other people whose bills and debts the transactions pay, or whose accounts the user's own
    // transfers moved money with, by user
    private readonly Dictionary<Guid, string> _ownerNames;

    // Who owns the other half's account, for the user's own transfers with someone else's account, by account
    private readonly Dictionary<Guid, Guid> _partnerAccountOwners;

    private TransactionMapper(IReadOnlyDictionary<Guid, string> customTags, Dictionary<Guid, string> accountNames,
        Dictionary<Guid, string> householdMemberNames, Dictionary<Guid, BankTransaction> partners,
        Dictionary<Guid, string> ruleNames, Dictionary<Guid, Bill> bills, Dictionary<Guid, Debt> debts,
        List<SavingsGoal> savingsGoals, TimeZoneInfo timeZone, AccountsInView view, Dictionary<Guid, string> sharedBy,
        Dictionary<Guid, string> ownerNames, Dictionary<Guid, Guid> partnerAccountOwners)
    {
        _customTags = customTags;
        _accountNames = accountNames;
        _householdMemberNames = householdMemberNames;
        _partners = partners;
        _ruleNames = ruleNames;
        _bills = bills;
        _debts = debts;
        _savingsGoals = savingsGoals;
        _timeZone = timeZone;
        _view = view;
        _sharedBy = sharedBy;
        _ownerNames = ownerNames;
        _partnerAccountOwners = partnerAccountOwners;
    }

    /// <summary>
    /// A mapper for the transactions in <paramref name="view"/>, with the tag names they can carry, see
    /// <see cref="TagNames"/>.
    /// </summary>
    /// <param name="transactions">What will be mapped. Partners outside it are loaded.</param>
    /// <param name="timeZone">Where the user is, for the day a tag savings goal tracks from.</param>
    public static async Task<TransactionMapper> LoadAsync(FinanceTrackerContext context, AccountsInView view,
        IReadOnlyCollection<BankTransaction> transactions, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        Dictionary<Guid, string> customTags = await TagNames.LoadAsync(context, view, cancellationToken);
        return await LoadAsync(context, view, customTags, transactions, timeZone, cancellationToken);
    }

    /// <summary>
    /// A mapper for the transactions in <paramref name="view"/>, the user's own and those of accounts shared with
    /// them. A shared transaction's tags, bills, debts, rules and household members are its owner's, so their names
    /// come from whoever owns them, as do those the other person put on the user's own and shared with them. Transfer
    /// partners only from accounts in view, never another's private account.
    /// </summary>
    /// <param name="customTags">Custom classifications by id, for the tags, the sharers' included.</param>
    /// <param name="transactions">What will be mapped. Partners outside it are loaded.</param>
    /// <param name="timeZone">Where the user is, for the day a tag savings goal tracks from.</param>
    public static async Task<TransactionMapper> LoadAsync(FinanceTrackerContext context, AccountsInView view,
        IReadOnlyDictionary<Guid, string> customTags, IReadOnlyCollection<BankTransaction> transactions,
        TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        List<Guid> owners = [view.UserId, .. view.SharedOwnerIds];

        Dictionary<Guid, string> accountNames = await context.BankAccounts.InView(view)
            .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);

        Dictionary<Guid, string> householdMemberNames =
            await LoadHouseholdMemberNamesAsync(context, owners, transactions, cancellationToken);

        Dictionary<Guid, BankTransaction> partners =
            await LoadPartnersAsync(context, view, transactions, cancellationToken);

        Dictionary<Guid, string> ruleNames = await LoadRuleNamesAsync(context, owners, transactions, cancellationToken);

        Dictionary<Guid, Bill> bills = await LoadBillsAsync(context, view.UserId, owners, transactions,
            cancellationToken);

        Dictionary<Guid, Debt> debts = await LoadDebtsAsync(context, view.UserId, owners, transactions,
            cancellationToken);

        // Only their own goals count transactions, sharing goals comes later
        List<SavingsGoal> savingsGoals = await LoadSavingsGoalsAsync(context, view.UserId, cancellationToken);

        Dictionary<Guid, string> sharedBy = await view.SharedByAsync(context, cancellationToken);

        Dictionary<Guid, Guid> partnerAccountOwners =
            await LoadPartnerAccountOwnersAsync(context, view, transactions, cancellationToken);

        Dictionary<Guid, string> ownerNames = await LoadOwnerNamesAsync(context, view.UserId,
            [
                .. bills.Values.Select(static x => x.UserId), .. debts.Values.Select(static x => x.UserId),
                .. partnerAccountOwners.Values
            ],
            cancellationToken);

        return new TransactionMapper(customTags, accountNames, householdMemberNames, partners, ruleNames, bills,
            debts, savingsGoals, timeZone, view, sharedBy, ownerNames, partnerAccountOwners);
    }

    /// <summary>
    /// Who owns the account at the other end of the user's own transfers that moved money with someone else's
    /// account, by account. Only their owner, not the account itself unless it's shared with them.
    /// </summary>
    private static async Task<Dictionary<Guid, Guid>> LoadPartnerAccountOwnersAsync(FinanceTrackerContext context,
        AccountsInView view, IReadOnlyCollection<BankTransaction> transactions, CancellationToken cancellationToken)
    {
        // A hidden copy filling a gap stands for its shown copy, whoever's that is
        List<Guid> accountIds = transactions
            .Where(x => x.UserId == view.UserId)
            .Select(static x => x.Transfer?.PartnerAccountId)
            .OfType<Guid>()
            .Select(view.ShownAs)
            .Where(x => !view.OwnAccountIds.Contains(x))
            .Distinct()
            .ToList();

        if (accountIds.Count == 0)
        {
            return [];
        }

        return await context.BankAccounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.UserId, cancellationToken);
    }

    /// <summary>
    /// The first and last names of <paramref name="userIds"/> other than the user, by user, empty when there are none.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> LoadOwnerNamesAsync(FinanceTrackerContext context,
        Guid userId, List<Guid> userIds, CancellationToken cancellationToken)
    {
        List<Guid> others = userIds.Where(x => x != userId).Distinct().ToList();
        if (others.Count == 0)
        {
            return [];
        }

        return await context.Users.AsNoTracking()
            .Where(x => others.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => $"{x.FirstName} {x.LastName}".Trim(), cancellationToken);
    }

    /// <summary>
    /// Household members' full names by id, empty when none of <paramref name="transactions"/> moved money with one.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> LoadHouseholdMemberNamesAsync(FinanceTrackerContext context,
        List<Guid> owners, IReadOnlyCollection<BankTransaction> transactions, CancellationToken cancellationToken)
    {
        if (!transactions.Any(static x => x.Transfer?.PartnerHouseholdMemberId is not null))
        {
            return [];
        }

        // Names are encrypted, so they're joined up once decrypted
        List<HouseholdMember> members = await context.HouseholdMembers.OwnedByAny(owners).AsNoTracking()
            .ToListAsync(cancellationToken);

        return members.ToDictionary(static x => x.Id, static x => $"{x.FirstName} {x.LastName}");
    }

    /// <summary>
    /// Rule names by id, empty when no rule tagged or decided any of <paramref name="transactions"/>.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> LoadRuleNamesAsync(FinanceTrackerContext context,
        List<Guid> owners, IReadOnlyCollection<BankTransaction> transactions, CancellationToken cancellationToken)
    {
        if (!transactions.Any(static x => x.RuleTags.Count > 0 || x.Transfer?.RuleId is not null ||
                                          x.BillPayment?.RuleId is not null ||
                                          x.DebtPayment?.RuleId is not null ||
                                          x.HiddenClassifications.Any(static hidden => !hidden.HiddenByUser)))
        {
            return [];
        }

        // Names are encrypted, so they're read whole and decrypted
        List<Rule> rules = await context.Rules.OwnedByAny(owners).AsNoTracking().ToListAsync(cancellationToken);

        return rules.ToDictionary(static x => x.Id, static x => x.Name);
    }

    /// <summary>
    /// The owners' bills, and those shared with the user, by id, for their names and whether each is a subscription,
    /// empty when none of <paramref name="transactions"/> pays a bill.
    /// </summary>
    private static async Task<Dictionary<Guid, Bill>> LoadBillsAsync(FinanceTrackerContext context, Guid userId,
        List<Guid> owners, IReadOnlyCollection<BankTransaction> transactions, CancellationToken cancellationToken)
    {
        if (!transactions.Any(static x => x.BillPayment?.BillId is not null))
        {
            return [];
        }

        // Names are encrypted, so they're read whole and decrypted
        IQueryable<Guid> shared = context.SharedWith(userId, ShareItemType.Bill);
        List<Bill> bills = await context.Bills.OwnedByAnyOrShared(owners, shared).AsNoTracking()
            .ToListAsync(cancellationToken);

        return bills.ToDictionary(static x => x.Id);
    }

    /// <summary>
    /// The user's goals that transactions can count towards, those backed by an account or tracking tags.
    /// </summary>
    private static async Task<List<SavingsGoal>> LoadSavingsGoalsAsync(FinanceTrackerContext context, Guid userId,
        CancellationToken cancellationToken) =>
        await context.SavingsGoals.OwnedBy(userId).AsNoTracking()
            .Where(static x => x.Source != SavingsGoalSource.Manual)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The owners' debts, and those shared with the user, by id, for their names and whose they are, empty when none
    /// of <paramref name="transactions"/> pays off a debt.
    /// </summary>
    private static async Task<Dictionary<Guid, Debt>> LoadDebtsAsync(FinanceTrackerContext context, Guid userId,
        List<Guid> owners, IReadOnlyCollection<BankTransaction> transactions, CancellationToken cancellationToken)
    {
        if (!transactions.Any(static x => x.DebtPayment?.DebtId is not null))
        {
            return [];
        }

        // Names are encrypted, so they're read whole and decrypted
        IQueryable<Guid> shared = context.SharedWith(userId, ShareItemType.Debt);
        List<Debt> debts = await context.Debts.OwnedByAnyOrShared(owners, shared).AsNoTracking()
            .ToListAsync(cancellationToken);

        return debts.ToDictionary(static x => x.Id);
    }

    /// <summary>
    /// The other halves of <paramref name="transactions"/>' transfers by id, the ones among them included and the
    /// rest loaded.
    /// </summary>
    private static async Task<Dictionary<Guid, BankTransaction>> LoadPartnersAsync(FinanceTrackerContext context,
        AccountsInView view, IReadOnlyCollection<BankTransaction> transactions, CancellationToken cancellationToken)
    {
        Dictionary<Guid, BankTransaction> partners = transactions.ToDictionary(static x => x.Id);

        List<Guid> missing = transactions
            .Select(static x => x.Transfer?.PartnerTransactionId)
            .OfType<Guid>()
            .Where(x => !partners.ContainsKey(x))
            .Distinct()
            .ToList();

        if (missing.Count == 0)
        {
            return partners;
        }

        List<BankTransaction> loaded = await context.BankTransactions.InView(view).AsNoTracking()
            .Where(x => missing.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (BankTransaction partner in loaded)
        {
            partners[partner.Id] = partner;
        }

        return partners;
    }

    public Transaction ToContract(BankTransaction transaction) => new()
    {
        Amount = transaction.Amount,
        Currency = transaction.Currency,
        Description = _view.DescriptionOf(transaction),
        Pending = transaction.IsPending,
        TransactionCategory = transaction.Category,
        TransactionId = transaction.Id,
        TransactionTime = transaction.OccurredAt,
        TransactionType = transaction.Type.ToString(),
        Tags = TransactionCriteria.GetTags(transaction, _customTags).ToList(),
        CountsAsIncomeOrSpending = transaction.CountsAsIncomeOrSpending,
        AccountId = _view.ShownAs(transaction.AccountId),
        AccountName = _accountNames.GetValueOrDefault(_view.ShownAs(transaction.AccountId), string.Empty),
        SharedBy = _sharedBy.GetValueOrDefault(_view.ShownAs(transaction.AccountId)),
        IsReadOnly = _view.IsReadOnly(transaction.AccountId),
        Transfer = TransferOf(transaction),
        Bill = ToContract(transaction.BillPayment),
        Debt = ToContract(transaction.DebtPayment),
        TagsFromRules = transaction.RuleTags
            .Where(x => _customTags.ContainsKey(x.ClassificationId))
            .Select(x => new TransactionRuleTag
            {
                Tag = _customTags[x.ClassificationId],
                RuleId = x.RuleId,
                RuleName = _ruleNames.GetValueOrDefault(x.RuleId, string.Empty)
            })
            .ToList(),
        BankClassifications = transaction.VisibleProviderClassifications.ToList(),
        HiddenBankClassifications = transaction.HiddenClassifications
            .Select(x => new TransactionHiddenClassification
            {
                Classification = x.Classification,
                RuleName = x.RuleId is { } ruleId ? _ruleNames.GetValueOrDefault(ruleId, string.Empty) : null
            })
            .ToList(),
        CustomTags = transaction.CustomClassificationIds
            .Select(x => _customTags.GetValueOrDefault(x))
            .OfType<string>()
            .ToList(),
        SavingsGoals = _savingsGoals
            .Where(x => x.Counts(transaction, _timeZone))
            .Select(static x => new TransactionSavingsGoal { SavingsGoalId = x.Id, Name = x.Name })
            .OrderBy(static x => x.Name)
            .ToList(),
        Splits = transaction.Splits.Select(ToContract).ToList()
    };

    private TransactionSplitPart ToContract(TransactionSplit split) => new()
    {
        Amount = split.Amount,
        TagId = split.TagId,
        Tag = split.TagId is { } tagId ? _customTags.GetValueOrDefault(tagId) : null,
        Note = split.Note
    };

    private TransactionTransfer? TransferOf(BankTransaction transaction)
    {
        if (transaction.Transfer is not { } transfer)
        {
            return null;
        }

        BankTransaction? partner = transfer.PartnerTransactionId is { } partnerId
            ? _partners.GetValueOrDefault(partnerId)
            : null;

        return new TransactionTransfer
        {
            State = transfer.Decision switch
            {
                TransferDecision.Transfer => TransferState.Transfer,
                TransferDecision.NotATransfer => TransferState.NotATransfer,
                _ => TransferState.NeedsReview
            },
            Reason = transfer.Reason is { } reason ? (TransferMatchReason)(int)reason : null,
            DecidedBy = transfer.Source switch
            {
                DecisionSource.User => TransferDecidedBy.User,
                DecisionSource.Rule => TransferDecidedBy.Rule,
                _ => TransferDecidedBy.FinTrack
            },
            PartnerTransactionId = partner?.Id,
            PartnerDescription = partner?.Description,
            PartnerTime = partner?.OccurredAt,
            PartnerAccountId = transfer.PartnerAccountId is { } partnerAccountId
                ? _view.ShownAs(partnerAccountId)
                : null,
            PartnerAccountName = transfer.PartnerAccountId is { } accountId
                ? _accountNames.GetValueOrDefault(_view.ShownAs(accountId))
                : null,
            PartnerHouseholdMemberName = transfer.PartnerHouseholdMemberId is { } memberId
                ? _householdMemberNames.GetValueOrDefault(memberId)
                : null,
            PartnerOwnerName = PartnerOwnerName(transaction),
            RuleName = transfer.RuleId is { } ruleId ? _ruleNames.GetValueOrDefault(ruleId) : null
        };
    }

    private TransactionBill? ToContract(BillPayment? payment)
    {
        if (payment is null)
        {
            return null;
        }

        return new TransactionBill
        {
            BillId = payment.BillId,
            BillName = Bill(payment)?.Name,
            IsSubscription = Bill(payment)?.Source == BillSource.Detected,
            DecidedBy = payment.Source switch
            {
                DecisionSource.User => TransferDecidedBy.User,
                DecisionSource.Rule => TransferDecidedBy.Rule,
                _ => TransferDecidedBy.FinTrack
            },
            RuleName = payment.RuleId is { } ruleId ? _ruleNames.GetValueOrDefault(ruleId) : null,
            OwnerName = OwnerName(Bill(payment)?.UserId)
        };
    }

    /// <summary>
    /// Whose account the other half of the user's own transfer is on, when it's someone else's.
    /// </summary>
    private string? PartnerOwnerName(BankTransaction transaction)
    {
        // Only on their own, the other person's say nothing of who else it moved money with
        if (transaction.UserId != _view.UserId || transaction.Transfer?.PartnerAccountId is not { } accountId ||
            !_partnerAccountOwners.TryGetValue(_view.ShownAs(accountId), out Guid ownerId))
        {
            return null;
        }

        return OwnerName(ownerId);
    }

    /// <summary>
    /// Whose it is when it isn't the user's, null when it's theirs or isn't known.
    /// </summary>
    private string? OwnerName(Guid? ownerId)
    {
        if (ownerId is not { } id || id == _view.UserId)
        {
            return null;
        }

        return _ownerNames.GetValueOrDefault(id);
    }

    private Bill? Bill(BillPayment payment)
    {
        if (payment.BillId is not { } billId)
        {
            return null;
        }

        return _bills.GetValueOrDefault(billId);
    }

    private TransactionDebt? ToContract(DebtPayment? payment)
    {
        if (payment is null)
        {
            return null;
        }

        Debt? debt = payment.DebtId is { } debtId ? _debts.GetValueOrDefault(debtId) : null;
        return new TransactionDebt
        {
            DebtId = payment.DebtId,
            DebtName = debt?.Name,
            DecidedBy = DebtMapper.DecidedBy(payment.Source),
            RuleName = payment.RuleId is { } ruleId ? _ruleNames.GetValueOrDefault(ruleId) : null,
            OwnerName = OwnerName(debt?.UserId)
        };
    }
}
