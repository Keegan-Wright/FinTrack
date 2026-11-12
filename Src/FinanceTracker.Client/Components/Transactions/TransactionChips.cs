using FinanceTracker.Client.Components.Bills;
using FinanceTracker.Client.Components.Debts;
using FinanceTracker.Client.Components.Rules;
using FinanceTracker.Contracts.Transactions;
using MudBlazor;

namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// What a transaction is for and how it's tagged, the bill, subscription, debt and savings goals it pays or
/// counts towards first, then whether it's split, the user's tags, then the bank's classifications.
/// </summary>
public static class TransactionChips
{
    /// <param name="linksOnly">Only the bill, subscription, debt, goals and split, for the compact phone list.</param>
    public static IReadOnlyList<TransactionChip> For(Transaction transaction, bool linksOnly = false)
    {
        List<TransactionChip> chips = [];

        if (Bill(transaction.Bill) is { } bill)
        {
            chips.Add(bill);
        }

        if (Debt(transaction.Debt) is { } debt)
        {
            chips.Add(debt);
        }

        chips.AddRange(transaction.SavingsGoals.Select(Goal));

        // On the phone list too, the amount shown there isn't all one thing
        if (transaction.Splits.Count > 0)
        {
            chips.Add(new TransactionChip($"Split {transaction.Splits.Count} ways", TransactionChipKind.Tag,
                Icons.Material.Outlined.CallSplit, null, SplitText.Summary(transaction)));
        }

        if (linksOnly)
        {
            return chips;
        }

        chips.AddRange(transaction.CustomTags.Select(tag => Tag(transaction, tag)));
        chips.AddRange(transaction.BankClassifications.Select(static x =>
            new TransactionChip(x, TransactionChipKind.Bank, null, null, "From your bank")));

        return chips;
    }

    /// <summary>
    /// Null when it pays no bill, or the user said it pays none.
    /// </summary>
    private static TransactionChip? Bill(TransactionBill? bill)
    {
        if (bill?.BillName is not { } name)
        {
            return null;
        }

        string reason = LowerFirst(BillText.Reason(bill));

        // Another person's bill isn't on the user's Bills page, so it's only named
        string? href = bill.OwnerName is null ? "Bills" : null;
        if (bill.IsSubscription)
        {
            return new TransactionChip(name, TransactionChipKind.Link, Icons.Material.Outlined.Autorenew, href,
                $"Pays {BillText.Whose(bill.OwnerName)} subscription {name}, {reason}");
        }

        return new TransactionChip(name, TransactionChipKind.Link, Icons.Material.Outlined.EventRepeat, href,
            $"Pays {name}, {reason}");
    }

    /// <summary>
    /// Null when it pays off no debt, or the user said it pays none.
    /// </summary>
    private static TransactionChip? Debt(TransactionDebt? debt)
    {
        if (debt?.DebtName is not { } name || debt.DebtId is not { } debtId)
        {
            return null;
        }

        string reason = LowerFirst(DebtText.Reason(debt.DecidedBy, debt.RuleName, debt.OwnerName));

        // Another person's debt has no page of the user's to open
        string? href = debt.OwnerName is null ? $"Debts/{debtId}" : null;
        return new TransactionChip(name, TransactionChipKind.Link, Icons.Material.Outlined.CreditScore, href,
            $"Pays off {name}, {reason}");
    }

    private static TransactionChip Goal(TransactionSavingsGoal goal) =>
        new(goal.Name, TransactionChipKind.Link, Icons.Material.Outlined.Savings, "SavingsGoals",
            $"Counts towards {goal.Name}");

    private static TransactionChip Tag(Transaction transaction, string tag)
    {
        if (RuleText.TaggedBy(transaction, tag) is { } ruleName)
        {
            return new TransactionChip(tag, TransactionChipKind.Tag, Icons.Material.Outlined.Rule, null,
                $"From your rule {ruleName}");
        }

        return new TransactionChip(tag, TransactionChipKind.Tag, Icons.Material.Outlined.Sell, null, null);
    }

    /// <summary>
    /// <c>Chosen by you</c> as <c>chosen by you</c> to follow a comma, leaving a rule's name as it is.
    /// </summary>
    private static string LowerFirst(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
