using System.Globalization;
using CsvHelper;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Infrastructure.Exports;

/// <summary>
/// One transaction as a line of the transactions CSV, in the columns' order.
/// </summary>
[CsvRecord]
public sealed partial record TransactionCsvRow
{
    /// <summary>
    /// The day it was where the user is.
    /// </summary>
    [CsvColumn("Date", Index = 0)]
    [CsvFormat("dd/MM/yyyy")]
    public DateOnly Date { get; init; }

    [CsvColumn("Account", Index = 1)]
    public string Account { get; init; } = string.Empty;

    [CsvColumn("Description", Index = 2)]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// With a dot, two places and no currency symbol, so a spreadsheet reads it as a number.
    /// </summary>
    [CsvColumn("Amount", Index = 3)]
    public decimal Amount { get; init; }

    [CsvColumn("Currency", Index = 4)]
    public string Currency { get; init; } = string.Empty;

    [CsvColumn("Type", Index = 5)]
    public string Type { get; init; } = string.Empty;

    [CsvColumn("Category", Index = 6)]
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Every tag, the bank's and the user's, separated by <c>; </c>.
    /// </summary>
    [CsvColumn("Tags", Index = 7)]
    public string Tags { get; init; } = string.Empty;

    [CsvColumn("Pending", Index = 8)]
    [CsvBooleanValues("Yes", "No")]
    public bool Pending { get; init; }

    /// <summary>
    /// Whether it was a transfer, empty when FinTrack never asked.
    /// </summary>
    [CsvColumn("Transfer", Index = 9)]
    public string Transfer { get; init; } = string.Empty;

    /// <summary>
    /// The account the money moved to or from, or the household member when their account isn't connected. Only for a
    /// transfer, for one waiting for review it would be FinTrack's guess.
    /// </summary>
    [CsvColumn("Transfer with", Index = 10)]
    public string TransferWith { get; init; } = string.Empty;

    /// <summary>
    /// The bill it pays, empty when it pays none.
    /// </summary>
    [CsvColumn("Bill", Index = 11)]
    public string Bill { get; init; } = string.Empty;

    /// <summary>
    /// The debt it pays off, empty when it pays none.
    /// </summary>
    [CsvColumn("Debt", Index = 12)]
    public string Debt { get; init; } = string.Empty;

    /// <summary>
    /// The savings goals it counts towards, separated by <c>; </c>.
    /// </summary>
    [CsvColumn("Savings goals", Index = 13)]
    public string SavingsGoals { get; init; } = string.Empty;

    /// <summary>
    /// Each part of a split transaction as its tag and amount, separated by <c>; </c>, e.g.
    /// <c>Groceries -60.00; Household -40.00</c>. Signed as <see cref="Amount"/> is, so the parts add up to it. Empty
    /// when it isn't split.
    /// </summary>
    [CsvColumn("Split", Index = 14)]
    public string Split { get; init; } = string.Empty;

    /// <param name="timeZone">Where the user is, so a transaction falls on the day it was for them.</param>
    public static TransactionCsvRow From(Transaction transaction, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(timeZone);

        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(transaction.TransactionTime, DateTimeKind.Utc), timeZone);

        return new TransactionCsvRow
        {
            Date = DateOnly.FromDateTime(local),
            Account = transaction.AccountName,
            Description = transaction.Description,
            Amount = ToPence(transaction.Amount),
            Currency = transaction.Currency,
            Type = transaction.TransactionType,
            Category = transaction.TransactionCategory,
            Tags = string.Join("; ", transaction.Tags),
            Pending = transaction.Pending,
            Transfer = TransferStatus(transaction.Transfer),
            TransferWith = TransferredWith(transaction.Transfer),
            Bill = transaction.Bill?.BillName ?? string.Empty,
            Debt = transaction.Debt?.DebtName ?? string.Empty,
            SavingsGoals = string.Join("; ", transaction.SavingsGoals.Select(static x => x.Name)),
            Split = string.Join("; ", transaction.Splits.Select(SplitPart))
        };
    }

    /// <summary>
    /// Rounded to the penny and written with two places, e.g. <c>-3.50</c> rather than <c>-3.5</c>. A decimal keeps
    /// the places it's given, and adding <c>0.00m</c> gives it at least two.
    /// </summary>
    private static decimal ToPence(decimal amount) => decimal.Round(amount, 2) + 0.00m;

    /// <summary>
    /// e.g. <c>Groceries -60.00</c>, <c>Untagged -10.00</c> for a part with no tag, and the part's note in brackets
    /// when it has one, e.g. <c>Household -40.00 (light bulbs)</c>.
    /// </summary>
    private static string SplitPart(TransactionSplitPart part)
    {
        string tag = string.IsNullOrWhiteSpace(part.Tag) ? "Untagged" : part.Tag;
        string text = $"{tag} {ToPence(part.Amount).ToString(CultureInfo.InvariantCulture)}";
        if (string.IsNullOrWhiteSpace(part.Note))
        {
            return text;
        }

        return $"{text} ({part.Note})";
    }

    private static string TransferredWith(TransactionTransfer? transfer)
    {
        if (transfer is not { State: TransferState.Transfer })
        {
            return string.Empty;
        }

        return transfer.PartnerAccountName ?? transfer.PartnerHouseholdMemberName ?? string.Empty;
    }

    private static string TransferStatus(TransactionTransfer? transfer) => transfer?.State switch
    {
        TransferState.Transfer => "Transfer",
        TransferState.NotATransfer => "Not a transfer",
        TransferState.NeedsReview => "Needs review",
        _ => string.Empty
    };
}
