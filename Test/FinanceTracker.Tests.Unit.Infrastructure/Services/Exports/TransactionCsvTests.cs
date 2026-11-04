using System.Text;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Infrastructure.Exports;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Exports;

public class TransactionCsvTests : UnitTestBase
{
    internal const string Header =
        "Date,Account,Description,Amount,Currency,Type,Category,Tags,Pending,Transfer,Transfer with,Bill,Debt," +
        "Savings goals,Split";

    [Test]
    public async Task Write_StartsWithAByteOrderMarkAndAHeaderNamingEachColumn()
    {
        // Act
        byte[] csv = await WriteAsync(TimeZoneInfo.Utc);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(csv[..3]).IsEquivalentTo(new byte[] { 0xEF, 0xBB, 0xBF });
            await Assert.That(Lines(csv)).IsEquivalentTo([Header]);
        }
    }

    [Test]
    public async Task Row_WritesEachColumnAsASpreadsheetReadsIt()
    {
        // Arrange
        Transaction transaction = Transaction("COFFEE SHOP", -3.5m, tags: ["Coffee", "Treats"],
            transfer: TransferState.NotATransfer);

        // Act
        string row = await RowAsync(transaction, TimeZoneInfo.Utc);

        // Assert
        await Assert.That(row)
            .IsEqualTo("04/10/2026,Main,COFFEE SHOP,-3.50,GBP,DEBIT,PURCHASE,Coffee; Treats,No,Not a transfer,,,,,");
    }

    [Test]
    public async Task Row_JustBeforeMidnightUtcInSummerTime_IsTheNextDayWhereTheUserIs()
    {
        // Arrange: 23:30 UTC on 4 Oct is 00:30 on 5 Oct in London
        TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        Transaction transaction = Transaction("LATE", -1m, at: new DateTime(2026, 10, 4, 23, 30, 0, DateTimeKind.Utc));

        // Act
        string row = await RowAsync(transaction, london);

        // Assert
        await Assert.That(row).StartsWith("05/10/2026,");
    }

    [Test]
    [Arguments("ACME, LTD", "\"ACME, LTD\"")]
    [Arguments("The \"big\" shop", "\"The \"\"big\"\" shop\"")]
    [Arguments("Line one\nline two", "\"Line one\nline two\"")]
    public async Task Row_TextWithACommaQuoteOrLineBreak_IsQuoted(string description, string expected)
    {
        // Act
        string row = await RowAsync(Transaction(description, -1m), TimeZoneInfo.Utc);

        // Assert
        await Assert.That(row.Split(',', 3)[2]).StartsWith(expected);
    }

    [Test]
    [Arguments("=HYPERLINK(\"http://example.test\")")]
    [Arguments("+44 PAYMENT")]
    [Arguments("-REFUND")]
    [Arguments("@SUM(A1)")]
    public async Task Row_TextASpreadsheetWouldRun_IsKeptAsText(string description)
    {
        // Act
        string row = await RowAsync(Transaction(description, -1m), TimeZoneInfo.Utc);

        // Assert
        await Assert.That(row).Contains("'" + description[0]);
    }

    [Test]
    public async Task Row_ANegativeAmount_StaysANumber()
    {
        // Act
        string row = await RowAsync(Transaction("SHOP", -1234.5m), TimeZoneInfo.Utc);

        // Assert
        await Assert.That(row).Contains(",-1234.50,");
    }

    [Test]
    [Arguments(TransferState.Transfer, "Transfer")]
    [Arguments(TransferState.NeedsReview, "Needs review")]
    public async Task Row_SaysWhetherItWasATransfer(TransferState state, string expected)
    {
        // Act
        string row = await RowAsync(Transaction("TO SAVINGS", -50m, transfer: state), TimeZoneInfo.Utc);

        // Assert
        await Assert.That(Column(row, "Transfer")).IsEqualTo(expected);
    }

    [Test]
    [Arguments(TransferState.Transfer, "Savings", null, "Savings")]
    [Arguments(TransferState.Transfer, null, "Alex", "Alex")]
    [Arguments(TransferState.NeedsReview, "Savings", null, "")]
    [Arguments(TransferState.NotATransfer, "Savings", null, "")]
    public async Task Row_ATransfer_SaysWhereTheMoneyMovedOnlyOnceItsATransfer(TransferState state, string? account,
        string? householdMember, string expected)
    {
        // Arrange
        Transaction transaction = Transaction("TO SAVINGS", -50m, transfer: state, transferAccount: account,
            transferHouseholdMember: householdMember);

        // Act
        string row = await RowAsync(transaction, TimeZoneInfo.Utc);

        // Assert
        await Assert.That(Column(row, "Transfer with")).IsEqualTo(expected);
    }

    [Test]
    public async Task Row_PendingWithNoTransferDecision_SaysSo()
    {
        // Act
        string row = await RowAsync(Transaction("SHOP", -1m, pending: true), TimeZoneInfo.Utc);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Column(row, "Pending")).IsEqualTo("Yes");
            await Assert.That(Column(row, "Transfer")).IsEmpty();
        }
    }

    [Test]
    public async Task Row_SaysWhichBillDebtAndSavingsGoalsItGoesTowards()
    {
        // Arrange
        Transaction transaction = Transaction("CAR FINANCE", -250m,
            bill: new TransactionBill { BillId = Guid.NewGuid(), BillName = "Car finance" },
            debt: new TransactionDebt { DebtId = Guid.NewGuid(), DebtName = "Car loan" },
            savingsGoals:
            [
                new TransactionSavingsGoal { SavingsGoalId = Guid.NewGuid(), Name = "Holiday" },
                new TransactionSavingsGoal { SavingsGoalId = Guid.NewGuid(), Name = "Rainy day" }
            ]);

        // Act
        string row = await RowAsync(transaction, TimeZoneInfo.Utc);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Column(row, "Bill")).IsEqualTo("Car finance");
            await Assert.That(Column(row, "Debt")).IsEqualTo("Car loan");
            await Assert.That(Column(row, "Savings goals")).IsEqualTo("Holiday; Rainy day");
        }
    }

    [Test]
    public async Task Row_ASplitTransaction_ListsEachPartSignedSoTheyAddUpToTheAmount()
    {
        // Arrange
        Transaction transaction = Transaction("SUPERMARKET", -100m, splits:
        [
            new TransactionSplitPart { Amount = -60m, Tag = "Groceries" },
            new TransactionSplitPart { Amount = -30m, Tag = "Household", Note = "light bulbs" },
            new TransactionSplitPart { Amount = -10m }
        ]);

        // Act
        string row = await RowAsync(transaction, TimeZoneInfo.Utc);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Column(row, "Amount")).IsEqualTo("-100.00");
            await Assert.That(Column(row, "Split"))
                .IsEqualTo("Groceries -60.00; Household -30.00 (light bulbs); Untagged -10.00");
        }
    }

    /// <summary>
    /// The field under <paramref name="column"/> in <paramref name="row"/>, for rows whose fields have no commas.
    /// </summary>
    private static string Column(string row, string column) =>
        row.Split(',')[Array.IndexOf(Header.Split(','), column)];

    /// <summary>
    /// The CSV of <paramref name="transactions"/>, as written.
    /// </summary>
    private async Task<byte[]> WriteAsync(TimeZoneInfo timeZone, params Transaction[] transactions)
    {
        using MemoryStream destination = new();
        await TransactionCsv.WriteAsync(transactions.ToAsyncEnumerable(), destination, timeZone, CancellationToken);
        return destination.ToArray();
    }

    /// <summary>
    /// The line written for <paramref name="transaction"/>, under the header.
    /// </summary>
    private async Task<string> RowAsync(Transaction transaction, TimeZoneInfo timeZone)
    {
        string[] lines = Lines(await WriteAsync(timeZone, transaction));
        await Assert.That(lines[0]).IsEqualTo(Header);
        return lines[1];
    }

    /// <summary>
    /// The lines of a written CSV, split on its CRLFs, so a quoted line break stays in its field.
    /// </summary>
    internal static string[] Lines(byte[] csv) =>
        Encoding.UTF8.GetString(csv).TrimStart('\uFEFF').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    internal static Transaction Transaction(string description, decimal amount, IEnumerable<string>? tags = null,
        TransferState? transfer = null, bool pending = false, DateTime? at = null, TransactionBill? bill = null,
        TransactionDebt? debt = null, List<TransactionSavingsGoal>? savingsGoals = null,
        List<TransactionSplitPart>? splits = null, string? transferAccount = null,
        string? transferHouseholdMember = null) => new()
    {
        Bill = bill,
        Debt = debt,
        SavingsGoals = savingsGoals ?? [],
        Splits = splits ?? [],
        TransactionId = Guid.NewGuid(),
        Description = description,
        TransactionType = amount < 0 ? "DEBIT" : "CREDIT",
        TransactionCategory = "PURCHASE",
        Amount = amount,
        Currency = "GBP",
        TransactionTime = at ?? new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc),
        Pending = pending,
        Tags = tags ?? [],
        AccountName = "Main",
        Transfer = transfer is { } state
            ? new TransactionTransfer
            {
                State = state, DecidedBy = TransferDecidedBy.FinTrack, Reason = TransferMatchReason.MirrorPair,
                PartnerAccountName = transferAccount, PartnerHouseholdMemberName = transferHouseholdMember
            }
            : null
    };
}
