using System.Globalization;
using FinanceTracker.Contracts.Transactions;
using MudBlazor;

namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// How a transaction's transfer is described to the user: the chip's label, who the money moved with and why.
/// </summary>
public static class TransferText
{
    public static string Label(TransactionTransfer transfer) => transfer.State switch
    {
        TransferState.Transfer when Partner(transfer, withDate: false) is { } partner => $"Transfer · {partner}",
        TransferState.Transfer => "Transfer",
        TransferState.NeedsReview => "Review transfer",
        _ => "Not a transfer"
    };

    /// <summary>
    /// The other half's description and date, e.g. <c>Rainy Day · 4 Oct 2026</c>. Null when there's no other half.
    /// </summary>
    public static string? OtherHalf(TransactionTransfer transfer) => transfer.PartnerDescription switch
    {
        null => null,

        var description when transfer.PartnerTime is { } time =>
            $"{description} · {time.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture)}",

        var description => description
    };

    /// <summary>
    /// How a transaction counts in a word, for sorting and where there's no chip <c>Transfer</c>, <c>Review</c>,
    /// <c>Refund</c>, <c>Income</c> or <c>Spending</c>. Money in the bank calls a purchase is a refund, as budgets
    /// treat it.
    /// </summary>
    public static string CountsAs(Transaction transaction)
    {
        if (transaction.Transfer?.State == TransferState.Transfer)
        {
            return "Transfer";
        }

        if (transaction.Transfer?.State == TransferState.NeedsReview)
        {
            return "Review";
        }

        if (transaction.Amount < 0)
        {
            return "Spending";
        }

        return IsRefund(transaction) ? "Refund" : "Income";
    }

    private static bool IsRefund(Transaction transaction) =>
        string.Equals(transaction.TransactionCategory, "PURCHASE", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Who or where the money moved to or from, e.g. <c>Rainy Day, 4 Oct</c> or <c>Jane Smith</c>. Null when
    /// nothing is known.
    /// </summary>
    public static string? Partner(TransactionTransfer transfer, bool withDate = true)
    {
        if (transfer.PartnerHouseholdMemberName is { } member)
        {
            return member;
        }

        if (transfer.PartnerAccountName is not { } account)
        {
            return null;
        }

        return withDate && transfer.PartnerTime is { } time
            ? $"{account}, {time.ToLocalTime().ToString("d MMM", CultureInfo.CurrentCulture)}"
            : account;
    }

    /// <summary>
    /// What stands in for the other half on the review page when it isn't waiting for review too.
    /// </summary>
    public static string MissingHalf(TransactionTransfer transfer)
    {
        if (OtherHalf(transfer) is { } otherHalf)
        {
            return $"Paired with {otherHalf}";
        }

        return transfer.Reason == TransferMatchReason.AmbiguousMirrorPair
            ? "Several transactions could be the other half"
            : "The other half isn't on any of your accounts";
    }

    /// <summary>
    /// Why it was decided, a sentence.
    /// </summary>
    public static string Reason(TransactionTransfer transfer)
    {
        if (transfer.DecidedBy == TransferDecidedBy.User)
        {
            return transfer.State == TransferState.NotATransfer
                ? "You said this counts as income or spending."
                : "You marked this as a transfer.";
        }

        if (transfer.DecidedBy == TransferDecidedBy.Rule)
        {
            return transfer.RuleName is null
                ? "Decided by one of your rules."
                : $"Decided by your rule {transfer.RuleName}.";
        }

        return transfer.Reason switch
        {
            TransferMatchReason.OwnAccountReference => "The bank's reference names another of your accounts.",
            TransferMatchReason.MirrorPair when transfer.State == TransferState.Transfer =>
                "The same amount moved the other way between your accounts, and the bank called it a transfer.",
            TransferMatchReason.MirrorPair =>
                "The same amount moved the other way between your accounts, but the bank didn't call it a " +
                "transfer. It still counts until you decide.",
            TransferMatchReason.AmbiguousMirrorPair =>
                "Several of the same amount moved the other way at the same time, so FinTrack didn't guess which. " +
                "It still counts until you decide.",
            TransferMatchReason.BankTransferLabel =>
                "The bank called it a transfer, but the other half isn't on any of your accounts. It still counts " +
                "until you decide.",
            TransferMatchReason.HouseholdMember => "Money to or from someone in your household.",
            _ => "Decided by a rule."
        };
    }

    /// <summary>
    /// The decisions that would change something, in the order they're offered.
    /// </summary>
    public static IEnumerable<TransferAction> Actions(TransactionTransfer? transfer)
    {
        if (transfer?.State != TransferState.Transfer)
        {
            yield return TransferAction.MarkAsTransfer;
        }

        if (transfer is not null && transfer.State != TransferState.NotATransfer)
        {
            yield return TransferAction.NotATransfer;
        }

        if (transfer?.DecidedBy == TransferDecidedBy.User)
        {
            yield return TransferAction.LetFinTrackDecide;
        }
    }

    public static string ActionLabel(TransferAction action) => action switch
    {
        TransferAction.MarkAsTransfer => "It's a transfer",
        TransferAction.NotATransfer => "Not a transfer",
        _ => "Let FinTrack decide"
    };

    /// <summary>
    /// The action's icon.
    /// </summary>
    public static string ActionIcon(TransferAction action) => action switch
    {
        TransferAction.MarkAsTransfer => Icons.Material.Outlined.SwapHoriz,
        TransferAction.NotATransfer => Icons.Material.Outlined.ShoppingBag,
        _ => Icons.Material.Outlined.AutoMode
    };
}
