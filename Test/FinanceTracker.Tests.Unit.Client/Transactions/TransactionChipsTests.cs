using Bunit;
using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class TransactionChipsTests : ComponentTestBase
{
    private static readonly Guid LoanId = Guid.NewGuid();

    [Test]
    public async Task For_PutsWhatItPaysFirstThenTagsThenTheBanks()
    {
        // Act
        IReadOnlyList<TransactionChip> chips = TransactionChips.For(Linked());

        // Assert
        await Assert.That(chips.Select(static x => (x.Text, x.Kind))).IsEquivalentTo(
            [
                ("Streaming", TransactionChipKind.Link), ("Car loan", TransactionChipKind.Link),
                ("Holiday", TransactionChipKind.Link), ("Treats", TransactionChipKind.Tag),
                ("Groceries", TransactionChipKind.Tag), ("Shopping", TransactionChipKind.Bank)
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task For_LinksEachToItsPageAndSaysWhy()
    {
        // Act
        Dictionary<string, TransactionChip> chips = TransactionChips.For(Linked()).ToDictionary(static x => x.Text);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(chips["Streaming"].Href).IsEqualTo("Bills");
            await Assert.That(chips["Streaming"].Title)
                .IsEqualTo("Pays your subscription Streaming, by your rule Streaming Co");
            await Assert.That(chips["Car loan"].Href).IsEqualTo($"Debts/{LoanId}");
            await Assert.That(chips["Car loan"].Title).IsEqualTo("Pays off Car loan, chosen by you");
            await Assert.That(chips["Holiday"].Href).IsEqualTo("SavingsGoals");
            await Assert.That(chips["Groceries"].Title).IsEqualTo("From your rule Weekly shop");
            await Assert.That(chips["Treats"].Title).IsNull();
        }
    }

    [Test]
    public async Task For_ABillThatIsntASubscription_ReadsAsABill()
    {
        // Arrange
        Transaction rent = TransactionListTests.Transaction("RENT", -650m, bill: new TransactionBill
        {
            BillId = Guid.NewGuid(), BillName = "Rent", DecidedBy = TransferDecidedBy.FinTrack
        });

        // Act
        TransactionChip chip = TransactionChips.For(rent).Single();

        // Assert
        await Assert.That(chip.Title).IsEqualTo("Pays Rent, matched by FinTrack");
    }

    [Test]
    public async Task For_SaidToPayNoBillOrDebt_HasNoChipForThem()
    {
        // Arrange
        Transaction transaction = TransactionListTests.Transaction("SHOP", -5m,
            bill: new TransactionBill { DecidedBy = TransferDecidedBy.User },
            debt: new TransactionDebt { DecidedBy = TransferDecidedBy.User });

        // Act & Assert
        await Assert.That(TransactionChips.For(transaction)).IsEmpty();
    }

    [Test]
    public async Task For_LinksOnly_LeavesOutTheTags()
    {
        // Act
        IReadOnlyList<TransactionChip> chips = TransactionChips.For(Linked(), linksOnly: true);

        // Assert
        await Assert.That(chips.Select(static x => x.Text)).IsEquivalentTo(["Streaming", "Car loan", "Holiday"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Render_LinksAreAnchorsToTheirPages()
    {
        // Act
        IRenderedComponent<TransactionChipList> cut = Render<TransactionChipList>(p => p
            .Add(x => x.Transaction, Linked()));

        // Assert
        await Assert.That(cut.FindAll("a").Select(static x => x.GetAttribute("href") ?? string.Empty))
            .IsEquivalentTo(["Bills", $"Debts/{LoanId}", "SavingsGoals"]);
    }

    [Test]
    public async Task Render_NothingToShow_RendersNothing() =>
        await Assert.That(Render<TransactionChipList>(p => p
                .Add(x => x.Transaction, TransactionListTests.Transaction("SHOP", -5m)))
            .Markup.Trim()).IsEmpty();

    private static Transaction Linked()
    {
        Transaction transaction = TransactionListTests.Transaction("STREAMCO", -9.99m,
            bill: new TransactionBill
            {
                BillId = Guid.NewGuid(), BillName = "Streaming", IsSubscription = true,
                DecidedBy = TransferDecidedBy.Rule, RuleName = "Streaming Co"
            },
            debt: new TransactionDebt { DebtId = LoanId, DebtName = "Car loan", DecidedBy = TransferDecidedBy.User });

        return new Transaction
        {
            TransactionId = transaction.TransactionId,
            Description = transaction.Description,
            TransactionType = transaction.TransactionType,
            TransactionCategory = transaction.TransactionCategory,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            TransactionTime = transaction.TransactionTime,
            Pending = false,
            Tags = ["Shopping", "Treats", "Groceries"],
            Bill = transaction.Bill,
            Debt = transaction.Debt,
            SavingsGoals = [new TransactionSavingsGoal { SavingsGoalId = Guid.NewGuid(), Name = "Holiday" }],
            CustomTags = ["Treats", "Groceries"],
            BankClassifications = ["Shopping"],
            TagsFromRules =
            [
                new TransactionRuleTag { Tag = "Groceries", RuleId = Guid.NewGuid(), RuleName = "Weekly shop" }
            ]
        };
    }
}
