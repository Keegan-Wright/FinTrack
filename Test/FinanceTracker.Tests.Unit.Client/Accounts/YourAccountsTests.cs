using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Account;

namespace FinanceTracker.Tests.Unit.Client.Accounts;

public class YourAccountsTests
{
    [Test]
    [Arguments("CREDIT_CARD", "1000", "Credit Card · ending 1000")]
    [Arguments("CREDIT_CARD", null, "Credit Card")]
    [Arguments("TRANSACTION", null, "Transaction")]
    public async Task TypeCaption_AddsACardsEnding(string accountType, string? cardEnding, string expected)
    {
        // Arrange
        AccountAndTransactions account = new()
        {
            AccountName = "Account",
            Logo = [],
            AccountType = accountType,
            AccountBalance = 0m,
            AvailableBalance = 0m,
            CardEnding = cardEnding
        };

        // Act
        string caption = YourAccounts.TypeCaption(account);

        // Assert
        await Assert.That(caption).IsEqualTo(expected);
    }
}
