using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
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

    private TransactionMapper(IReadOnlyDictionary<Guid, string> customTags, Dictionary<Guid, string> accountNames,
        Dictionary<Guid, string> householdMemberNames, Dictionary<Guid, BankTransaction> partners)
    {
        _customTags = customTags;
        _accountNames = accountNames;
        _householdMemberNames = householdMemberNames;
        _partners = partners;
    }

    /// <param name="customTags">The user's custom classifications by id, for the tags.</param>
    /// <param name="transactions">What will be mapped. Partners outside it are loaded.</param>
    public static async Task<TransactionMapper> LoadAsync(FinanceTrackerContext context, Guid userId,
        IReadOnlyDictionary<Guid, string> customTags, IReadOnlyCollection<BankTransaction> transactions,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, string> accountNames = await context.BankAccounts.OwnedBy(userId)
            .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);

        Dictionary<Guid, string> householdMemberNames =
            await LoadHouseholdMemberNamesAsync(context, userId, transactions, cancellationToken);

        Dictionary<Guid, BankTransaction> partners =
            await LoadPartnersAsync(context, userId, transactions, cancellationToken);

        return new TransactionMapper(customTags, accountNames, householdMemberNames, partners);
    }

    /// <summary>
    /// Household members' full names by id, empty when none of <paramref name="transactions"/> moved money with one.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> LoadHouseholdMemberNamesAsync(FinanceTrackerContext context,
        Guid userId, IReadOnlyCollection<BankTransaction> transactions, CancellationToken cancellationToken)
    {
        if (!transactions.Any(static x => x.Transfer?.PartnerHouseholdMemberId is not null))
        {
            return [];
        }

        // Names are encrypted, so they're joined up once decrypted
        List<HouseholdMember> members = await context.HouseholdMembers.OwnedBy(userId).AsNoTracking()
            .ToListAsync(cancellationToken);

        return members.ToDictionary(static x => x.Id, static x => $"{x.FirstName} {x.LastName}");
    }

    /// <summary>
    /// The other halves of <paramref name="transactions"/>' transfers by id, the ones among them included and the
    /// rest loaded.
    /// </summary>
    private static async Task<Dictionary<Guid, BankTransaction>> LoadPartnersAsync(FinanceTrackerContext context,
        Guid userId, IReadOnlyCollection<BankTransaction> transactions, CancellationToken cancellationToken)
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

        List<BankTransaction> loaded = await context.BankTransactions.OwnedBy(userId).AsNoTracking()
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
        Description = transaction.Description,
        Pending = transaction.IsPending,
        TransactionCategory = transaction.Category,
        TransactionId = transaction.Id,
        TransactionTime = transaction.OccurredAt,
        TransactionType = transaction.Type.ToString(),
        Tags = TransactionCriteria.GetTags(transaction, _customTags).ToList(),
        CountsAsIncomeOrSpending = transaction.CountsAsIncomeOrSpending,
        AccountId = transaction.AccountId,
        AccountName = _accountNames.GetValueOrDefault(transaction.AccountId, string.Empty),
        Transfer = ToContract(transaction.Transfer)
    };

    private TransactionTransfer? ToContract(TransferInfo? transfer)
    {
        if (transfer is null)
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
            PartnerAccountId = transfer.PartnerAccountId,
            PartnerAccountName = transfer.PartnerAccountId is { } accountId
                ? _accountNames.GetValueOrDefault(accountId)
                : null,
            PartnerHouseholdMemberName = transfer.PartnerHouseholdMemberId is { } memberId
                ? _householdMemberNames.GetValueOrDefault(memberId)
                : null
        };
    }
}
