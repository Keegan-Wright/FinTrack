using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Accounts;

public class YourAccountsTests : ComponentTestBase
{
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly INetWorthService _netWorthService = Substitute.For<INetWorthService>();
    private readonly ApplicationState _applicationState = new();

    public YourAccountsTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_accountService);
        Services.AddSingleton(_netWorthService);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTime(2026, 11, 4, 12, 0, 0,
            DateTimeKind.Utc)));
        _netWorthService.GetBalanceHistoriesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<AccountBalanceHistory>());
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
    }

    [Test]
    public async Task Synced_ShowsTheNewBalances()
    {
        // Arrange
        using CultureScope culture = new();
        _accountService.GetAccountsAndMostRecentTransactionsAsync(5, Arg.Any<CancellationToken>())
            .Returns(_ => new[] { Account(100m) }.ToAsyncEnumerable(), _ => new[] { Account(250m) }.ToAsyncEnumerable());
        IRenderedComponent<YourAccounts> cut = Render<YourAccounts>(p => p.AddCascadingValue(_applicationState));
        cut.WaitForState(() => cut.Markup.Contains("£100.00"));

        // Act
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("£250.00"));
        _netWorthService.Received(2).GetBalanceHistoriesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await Assert.That(cut.Markup).DoesNotContain("£100.00");
    }

    private static AccountAndTransactions Account(decimal balance) => new()
    {
        AccountId = Guid.Empty,
        AccountName = "Current",
        Logo = [],
        AccountType = "TRANSACTION",
        AccountBalance = balance,
        AvailableBalance = balance
    };

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
