using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.OpenBanking;

namespace FinanceTracker.Tests.Unit.Client.Connections;

public class ProviderSetupTests
{
    [Test]
    [Arguments(ConsentState.Active, "Access until 31 Jan 2099")]
    [Arguments(ConsentState.RenewSoon, "Access ends 31 Jan 2099, renew to keep syncing")]
    [Arguments(ConsentState.Lapsed, "Access ended 31 Jan 2099, renew to sync again")]
    public async Task ConsentText_SaysWhenAccessEndsAndWhatToDo(ConsentState state, string expected)
    {
        // Arrange
        using CultureScope culture = new();
        BankConnectionSummary connection = new()
        {
            ConnectionId = Guid.NewGuid(),
            Name = "Bank",
            Logo = [],
            ConsentExpiresAt = new DateTime(2099, 1, 31, 12, 0, 0, DateTimeKind.Utc),
            ConsentState = state
        };

        // Act
        string text = ProviderSetup.ConsentText(connection);

        // Assert
        await Assert.That(text).IsEqualTo(expected);
    }
}
