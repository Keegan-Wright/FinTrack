using FinanceTracker.Client.Components.Common;

namespace FinanceTracker.Tests.Unit.Client.Common;

public class CurrencyFormatterTests
{
    [Test]
    [Arguments(1234.5, "£1,234.50")]
    [Arguments(-14.86, "-£14.86")]
    [Arguments(0, "£0.00")]
    public async Task Format_UsesTheAppsCulture(decimal value, string expected)
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string formatted = CurrencyFormatter.Format(value);

        // Assert
        await Assert.That(formatted).IsEqualTo(expected);
    }
}
