using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Infrastructure.Exports.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfFormat;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Statements;

/// <summary>
/// Lays a <see cref="Statement"/> out: a summary of money in and out, spending by tag, bills, budgets, savings goals
/// and debts, then every transaction from a new page.
/// </summary>
public sealed class StatementDocument : FinTrackDocument
{
    private readonly Statement _statement;
    private readonly TimeZoneInfo _timeZone;

    public StatementDocument(Statement statement, TimeZoneInfo timeZone)
        : base(statement.MadeOn)
    {
        _statement = statement;
        _timeZone = timeZone;
    }

    protected override string Title => $"Statement for {_statement.Month.ToString("MMMM yyyy", Culture)}";

    protected override string Covers => DateRange(_statement.Start, _statement.End);

    protected override string CoversDetail =>
        _statement.FromPayDay ? "Pay day to pay day, all accounts" : "Calendar month, all accounts";

    protected override void ComposeContent(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(20);

            column.Item().Element(ComposeSummary);
            column.Item().Component(new PdfSection("Spending by tag",
                "A transaction with more than one tag counts towards each, and spending with none is Untagged",
                ComposeTags));
            column.Item().Component(new PdfSection("Bills", BillsCaption(), ComposeBills));
            column.Item().Component(new PdfSection("Budgets",
                $"Not in a budget: {Money(_statement.Budgets.Unbudgeted)}", ComposeBudgets));

            if (_statement.SavingsGoals.Count > 0 || _statement.Debts.Count > 0)
            {
                column.Item().Component(new PdfSection("Savings goals and debts",
                    $"As they stand on {LongDate(MadeOn)}", ComposeGoalsAndDebts));
            }

            column.Item().PageBreak();
            column.Item().Component(new PdfSection("Transactions",
                $"{TransactionCount(_statement.Transactions.Count)} across all your accounts, oldest first",
                ComposeTransactions));
        });
    }

    private void ComposeSummary(IContainer container)
    {
        decimal moneyIn = _statement.Totals.TotalIn;
        decimal moneyOut = -_statement.Totals.TotalOut;
        container.Component(new InOutSummary(moneyIn, moneyOut, "Left over", SummaryNotes().ToList()));
    }

    private IEnumerable<string> SummaryNotes()
    {
        if (_statement.IsCurrent)
        {
            yield return $"So far, the period runs until {LongDate(_statement.End)}.";
        }

        decimal movedIn = _statement.Totals.TransferredIn;
        decimal movedOut = -_statement.Totals.TransferredOut;
        if (movedIn != 0 || movedOut != 0)
        {
            yield return $"Not counted: {Money(movedIn)} moved in and {Money(movedOut)} moved out between your own " +
                         "accounts or your household.";
        }
    }

    private void ComposeTags(IContainer container)
    {
        decimal untagged = _statement.Untagged;
        if (_statement.Tags.Count == 0 && untagged == 0)
        {
            container.EmptyNote("Nothing was spent in this period.");
            return;
        }

        decimal most = MostSpentOnOneLine(untagged);
        container.Column(column =>
        {
            column.Spacing(5);
            foreach (SpentInCategoryReport tag in _statement.Tags)
            {
                decimal spent = -tag.TotalOut;
                column.Item().Component(new BarRow(tag.Category, spent / most, Money(spent), MoneyOut));
            }

            // Last, as it isn't a tag
            if (untagged > 0)
            {
                column.Item().Component(new BarRow("Untagged", untagged / most, Money(untagged), MoneyOut));
            }
        });
    }

    /// <summary>
    /// The longest bar, the most spent on one tag or with no tag, which the others are drawn against.
    /// </summary>
    private decimal MostSpentOnOneLine(decimal untagged)
    {
        if (_statement.Tags.Count == 0)
        {
            return untagged;
        }

        return Math.Max(-_statement.Tags[0].TotalOut, untagged);
    }

    private void ComposeBills(IContainer container)
    {
        List<(Bill Bill, BillPeriod Period)> due = _statement.Bills.Bills
            .SelectMany(static x => x.Periods.Select(period => (x.Bill, period)))
            .OrderBy(static x => x.period.Due)
            .ToList();
        if (due.Count == 0)
        {
            container.EmptyNote("No bills were due in this period.");
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.ConstantColumn(60);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(60);
            });

            table.Header(header =>
            {
                header.Cell().TableHeaderCell("Bill");
                header.Cell().TableHeaderCell("Due");
                header.Cell().TableHeaderCell("Expected", alignRight: true);
                header.Cell().TableHeaderCell("Paid", alignRight: true);
                header.Cell().TableHeaderCell("Status", alignRight: true);
            });

            foreach ((Bill bill, BillPeriod period) in due)
            {
                table.Cell().TableCell().Text(bill.Name);
                table.Cell().TableCell().Text(ShortDate(period.Due));
                table.Cell().TableCell().AlignRight().Text(Money(Math.Abs(period.Expected)));
                table.Cell().TableCell().AlignRight().Text(period.Paid == 0 ? "–" : Money(Math.Abs(period.Paid)));
                table.Cell().TableCell().AlignRight().Text(BillStateText(period.State)).SemiBold()
                    .FontColor(BillStateColour(period.State));
            }
        });
    }

    private void ComposeBudgets(IContainer container)
    {
        if (_statement.Budgets.Budgets.Count == 0)
        {
            container.EmptyNote("You have no budgets.");
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(3);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
            });

            table.Header(header =>
            {
                header.Cell().TableHeaderCell("Budget");
                header.Cell().TableHeaderCell("");
                header.Cell().TableHeaderCell("Available", alignRight: true);
                header.Cell().TableHeaderCell("Spent", alignRight: true);
                header.Cell().TableHeaderCell("Left", alignRight: true);
            });

            foreach (BudgetStatus budget in _statement.Budgets.Budgets)
            {
                decimal share = budget.Available <= 0 ? 1 : budget.Spent / budget.Available;
                table.Cell().TableCell().Text(budget.Name);
                table.Cell().TableCell().AlignMiddle().Component(new ProgressBar(share, BudgetColour(budget)));
                table.Cell().TableCell().AlignRight().Text(Money(budget.Available));
                table.Cell().TableCell().AlignRight().Text(Money(budget.Spent));
                table.Cell().TableCell().AlignRight().Text(Money(budget.Remaining))
                    .FontColor(budget.Remaining < 0 ? MoneyOut : TextPrimary);
            }
        });
    }

    private void ComposeGoalsAndDebts(IContainer container)
    {
        container.Row(row =>
        {
            row.Spacing(20);

            if (_statement.SavingsGoals.Count > 0)
            {
                row.RelativeItem().Column(goals =>
                {
                    goals.Spacing(8);
                    foreach (SavingsGoal goal in _statement.SavingsGoals)
                    {
                        goals.Item().Component(new LabelledProgress(goal.Name, GoalText(goal), GoalShare(goal),
                            Primary));
                    }
                });
            }

            if (_statement.Debts.Count > 0)
            {
                row.RelativeItem().Column(debts =>
                {
                    debts.Spacing(8);
                    foreach (Debt debt in _statement.Debts)
                    {
                        string detail = $"{Money(debt.Owed)} owed, {debt.PaidOffPercent:0}% paid off";
                        debts.Item().Component(new LabelledProgress(debt.Name, detail, debt.PaidOffPercent / 100,
                            Info));
                    }
                });
            }
        });
    }

    private void ComposeTransactions(IContainer container)
    {
        if (_statement.Transactions.Count == 0)
        {
            container.EmptyNote("No transactions in this period.");
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(50);
                columns.RelativeColumn(4);
                columns.RelativeColumn(3);
                columns.ConstantColumn(70);
            });

            table.Header(header =>
            {
                header.Cell().TableHeaderCell("Date");
                header.Cell().TableHeaderCell("Description");
                header.Cell().TableHeaderCell("Tags");
                header.Cell().TableHeaderCell("Amount", alignRight: true);
            });

            foreach (Transaction transaction in _statement.Transactions)
            {
                table.Cell().TableCell().Text(ShortDate(LocalDay(transaction)));
                table.Cell().TableCell().Column(description =>
                {
                    description.Item().Text(transaction.Description);
                    description.Item().Text(transaction.AccountName).FontSize(SmallSize).FontColor(TextSecondary);
                });
                table.Cell().TableCell().Text(TransactionNotes(transaction)).FontSize(SmallSize)
                    .FontColor(TextSecondary);
                table.Cell().TableCell().AlignRight().Text(Money(transaction.Amount))
                    .FontColor(AmountColour(transaction.Amount));
            }
        });
    }

    private string BillsCaption()
    {
        BillMonthOverview bills = _statement.Bills;
        return $"{Money(Math.Abs(bills.Paid))} paid of {Money(Math.Abs(bills.Due))} due";
    }

    private static string BillStateText(BillState state) => state switch
    {
        BillState.Paid => "Paid",
        BillState.Late => "Late",
        BillState.Changed => "Price changed",
        _ => "Upcoming"
    };

    private static string BillStateColour(BillState state) => state switch
    {
        BillState.Paid => MoneyIn,
        BillState.Late => MoneyOut,
        BillState.Changed => Warning,
        _ => TextSecondary
    };

    private static string BudgetColour(BudgetStatus budget)
    {
        if (budget.Remaining < 0)
        {
            return MoneyOut;
        }

        if (!budget.OnPace)
        {
            return Warning;
        }

        return Primary;
    }

    private static string GoalText(SavingsGoal goal)
    {
        if (goal.Target <= 0)
        {
            return $"{Money(goal.Saved)} saved";
        }

        return $"{Money(goal.Saved)} of {Money(goal.Target)}";
    }

    /// <returns>Null for a goal with no target, as there's nothing to fill.</returns>
    private static decimal? GoalShare(SavingsGoal goal)
    {
        if (goal.Target <= 0)
        {
            return null;
        }

        return goal.Saved / goal.Target;
    }

    /// <summary>
    /// Its tags, then whether it's a transfer or pending, e.g. <c>Groceries, Household · Pending</c>.
    /// </summary>
    private static string TransactionNotes(Transaction transaction)
    {
        List<string> notes = [];
        string tags = string.Join(", ", transaction.Tags);
        if (tags.Length > 0)
        {
            notes.Add(tags);
        }

        if (transaction.Transfer is { State: TransferState.Transfer })
        {
            notes.Add("Transfer");
        }

        if (transaction.Pending)
        {
            notes.Add("Pending");
        }

        return string.Join(" · ", notes);
    }

    /// <summary>
    /// Money in green and money out red, as the transactions page and the other PDFs show amounts. Transfers too, the
    /// notes column says they're a transfer.
    /// </summary>
    private static string AmountColour(decimal amount)
    {
        if (amount > 0)
        {
            return MoneyIn;
        }

        if (amount < 0)
        {
            return MoneyOut;
        }

        return TextPrimary;
    }

    private DateOnly LocalDay(Transaction transaction)
    {
        DateTime utc = DateTime.SpecifyKind(transaction.TransactionTime, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone));
    }
}
