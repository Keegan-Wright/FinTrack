using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Tests.Unit.Domain.Debts;

[Category("Domain")]
public class DebtTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Test]
    [Arguments("", 5000, 9.9, 200)]
    [Arguments("Car loan", 0, 9.9, 200)]
    [Arguments("Car loan", 5000, -1, 200)]
    [Arguments("Car loan", 5000, 101, 200)]
    [Arguments("Car loan", 5000, 9.9, 0)]
    public async Task Constructor_InvalidNameBalanceRateOrPayment_Throws(string name, double startingBalance,
        double interestRate, double monthlyPayment) =>
        await Assert.That(() => new Debt(UserId, name, null, (decimal)startingBalance, new DateOnly(2026, 1, 1),
            (decimal)interestRate, (decimal)monthlyPayment)).Throws<ArgumentException>();

    [Test]
    public async Task Constructor_TrimsTheNameAndABlankLenderIsNone()
    {
        // Act
        Debt debt = new(UserId, "  Car loan  ", "   ", 5000m, new DateOnly(2026, 1, 1), 0m, 200m);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(debt.Name).IsEqualTo("Car loan");
            await Assert.That(debt.Lender).IsNull();
            await Assert.That(debt.AccountId).IsNull();
            await Assert.That(debt.PayOffGoal).IsNull();
        }
    }

    [Test]
    public async Task LinkAccount_TheUsersAccount_FollowsItUntilUnlinked()
    {
        // Arrange
        Debt debt = CarLoan();
        BankAccount card = new(UserId, Guid.NewGuid(), "card-1", "CREDIT", "Card", "GBP", BankAccountKind.Card);

        // Act
        debt.LinkAccount(card);
        Guid? linked = debt.AccountId;
        debt.UnlinkAccount();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(linked).IsEqualTo(card.Id);
            await Assert.That(debt.AccountId).IsNull();
        }
    }

    [Test]
    public async Task LinkAccount_AnotherUsersAccount_Throws()
    {
        // Arrange
        Debt debt = CarLoan();
        BankAccount theirs = new(Guid.NewGuid(), Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

        // Act & Assert
        await Assert.That(() => debt.LinkAccount(theirs)).Throws<InvalidOperationException>();
    }

    private static Debt CarLoan() => new(UserId, "Car loan", "Lender Co", 5000m, new DateOnly(2026, 1, 1), 9.9m, 200m);
}
