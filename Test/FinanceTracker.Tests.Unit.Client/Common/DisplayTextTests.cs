using FinanceTracker.Client.Components.Common;

namespace FinanceTracker.Tests.Unit.Client.Common;

public class DisplayTextTests
{
    [Test]
    [Arguments("BILL_PAYMENT", "Bill Payment")]
    [Arguments("DEBIT", "Debit")]
    [Arguments("direct_debit", "Direct Debit")]
    public async Task ToTitle_TidiesProviderText(string value, string expected)
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string title = DisplayText.ToTitle(value);

        // Assert
        await Assert.That(title).IsEqualTo(expected);
    }
}
