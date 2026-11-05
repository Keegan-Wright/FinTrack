using System.Text;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Infrastructure.Exports.Statements;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Exports;

public class StatementDocumentTests
{
    [Test]
    public async Task GeneratePdf_IsAPdf()
    {
        // Act
        byte[] pdf = new StatementDocument(Sample(), TimeZoneInfo.Utc).GeneratePdf();

        // Assert
        await Assert.That(Encoding.ASCII.GetString(pdf, 0, 5)).IsEqualTo("%PDF-");
    }

    [Test]
    public async Task GeneratePdf_TransactionsStartOnTheirOwnPage()
    {
        // Act
        int pages = PageCount(new StatementDocument(Sample(), TimeZoneInfo.Utc));

        // Assert
        await Assert.That(pages).IsEqualTo(2);
    }

    [Test]
    public async Task GeneratePdf_ManyTransactions_RunOntoMorePages()
    {
        // Arrange
        List<Transaction> transactions = Enumerable.Range(1, 120)
            .Select(static x => TransactionCsvTests.Transaction($"SHOP {x}", -x))
            .ToList();

        // Act
        int pages = PageCount(new StatementDocument(Sample(transactions), TimeZoneInfo.Utc));

        // Assert
        await Assert.That(pages).IsGreaterThan(2);
    }

    [Test]
    public async Task GeneratePdf_WithUntaggedSpending_StillLaysOut()
    {
        // Arrange
        Statement statement = Sample([TransactionCsvTests.Transaction("CORNER SHOP", -12.50m)]);

        // Act
        int pages = PageCount(new StatementDocument(statement, TimeZoneInfo.Utc));

        // Assert
        await Assert.That(pages).IsEqualTo(2);
    }

    [Test]
    public async Task GeneratePdf_NothingInThePeriod_StillLaysOut()
    {
        // Arrange
        Statement empty = new()
        {
            Month = new DateOnly(2026, 11, 1),
            Start = new DateOnly(2026, 11, 1),
            End = new DateOnly(2026, 11, 30),
            IsCurrent = true,
            MadeOn = new DateOnly(2026, 11, 4),
            Totals = new SpentInTimePeriod(),
            Tags = [],
            Bills = new BillMonthOverview(),
            Budgets = new BudgetMonthOverview(),
            SavingsGoals = [],
            Debts = [],
            Transactions = []
        };

        // Act
        int pages = PageCount(new StatementDocument(empty, TimeZoneInfo.Utc));

        // Assert
        await Assert.That(pages).IsEqualTo(2);
    }

    private static int PageCount(IDocument document) =>
        document.GenerateImages(new ImageGenerationSettings { RasterDpi = 10 }).Count();

    /// <summary>
    /// A statement for the pay period named after November 2026 with some of everything, placeholder names only.
    /// </summary>
    internal static Statement Sample(List<Transaction>? transactions = null) => new()
    {
        Month = new DateOnly(2026, 11, 1),
        Start = new DateOnly(2026, 10, 30),
        End = new DateOnly(2026, 11, 27),
        IsCurrent = false,
        MadeOn = new DateOnly(2026, 11, 28),
        Totals = new SpentInTimePeriod
        {
            TotalIn = 2450m, TotalOut = -1984.37m, TransferredIn = 300m, TransferredOut = -300m
        },
        Tags =
        [
            new SpentInCategoryReport { Category = "Rent", TotalOut = -850m },
            new SpentInCategoryReport { Category = "Groceries", TotalOut = -312.45m },
            new SpentInCategoryReport { Category = "Eating out", TotalOut = -96.20m },
            new SpentInCategoryReport { Category = "Transport", TotalOut = -64m }
        ],
        Bills = new BillMonthOverview
        {
            Due = 1012m,
            Paid = 962m,
            Bills =
            [
                Bill("Rent", 850m, 850m, BillState.Paid, 1),
                Bill("Energy", 112m, 112m, BillState.Paid, 3),
                Bill("Phone", 50m, 0m, BillState.Late, 20)
            ]
        },
        Budgets = new BudgetMonthOverview
        {
            Unbudgeted = 140.12m,
            Budgets =
            [
                new BudgetStatus
                {
                    Name = "Groceries", Available = 350m, Spent = 312.45m, Remaining = 37.55m, OnPace = true
                },
                new BudgetStatus
                {
                    Name = "Eating out", Available = 80m, Spent = 96.20m, Remaining = -16.20m, OnPace = false
                }
            ]
        },
        SavingsGoals =
        [
            new SavingsGoal { Id = Guid.NewGuid(), Name = "Holiday", Saved = 640m, Target = 1500m },
            new SavingsGoal { Id = Guid.NewGuid(), Name = "Rainy day", Saved = 2200m }
        ],
        Debts =
        [
            new Debt
            {
                Name = "Car loan", Owed = 4120m, PaidOffPercent = 38, Forecast = new DebtForecast()
            }
        ],
        Transactions = transactions ??
        [
            TransactionCsvTests.Transaction("EMPLOYER LTD", 2450m, at: new DateTime(2026, 10, 30, 9, 0, 0)),
            TransactionCsvTests.Transaction("LANDLORD", -850m, ["Rent"], at: new DateTime(2026, 11, 1, 8, 0, 0)),
            TransactionCsvTests.Transaction("TO SAVINGS", -300m, transfer: TransferState.Transfer,
                at: new DateTime(2026, 11, 2, 8, 0, 0)),
            TransactionCsvTests.Transaction("SUPERMARKET", -84.12m, ["Groceries", "Household"],
                at: new DateTime(2026, 11, 3, 18, 0, 0)),
            TransactionCsvTests.Transaction("COFFEE SHOP", -3.50m, ["Eating out"], pending: true,
                at: new DateTime(2026, 11, 26, 8, 0, 0))
        ]
    };

    private static BillStatus Bill(string name, decimal expected, decimal paid, BillState state, int dueDay) => new()
    {
        Bill = new Bill { Name = name, Payee = name.ToUpperInvariant(), AccountName = "Main" },
        Periods =
        [
            new BillPeriod
            {
                Due = new DateOnly(2026, 11, dueDay), Expected = expected, Paid = paid, State = state
            }
        ]
    };
}
