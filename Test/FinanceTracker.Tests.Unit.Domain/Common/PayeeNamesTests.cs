using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Tests.Unit.Domain.Common;

[Category("Domain")]
public class PayeeNamesTests
{
    private static readonly BankAccount Account = new(Guid.NewGuid(), Guid.NewGuid(), "acc-1", "TRANSACTION", "Main",
        "GBP");

    [Test]
    [Arguments("NETFLIX.COM 866-579", "NETFLIX.COM")]
    [Arguments("ACME PAYROLL PAY REF00P2118CXWF4", "ACME PAYROLL PAY")]
    [Arguments("ACME PAYROLL PAY REF00P2117R2R93", "ACME PAYROLL PAY")]
    [Arguments("7ELEVEN", "ELEVEN")]
    [Arguments("CAFE NERO", "CAFE NERO")]
    public async Task Name_DropsWordsWithADigitIn(string description, string expected)
    {
        // Act
        string name = PayeeNames.Name(Transaction(description));

        // Assert
        await Assert.That(name).IsEqualTo(expected);
    }

    private static BankTransaction Transaction(string description) =>
        BankTransaction.Import(Account, new TransactionDetails(Guid.NewGuid().ToString(), description,
            TransactionType.Credit, "CREDIT", 10m, "GBP", new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc),
            false, [], TransactionMetadata.None, false));
}
