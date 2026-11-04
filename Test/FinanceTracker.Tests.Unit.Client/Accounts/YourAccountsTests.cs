using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Components.Accounts;
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

    [Test]
    public async Task Render_AccountsAtTwoBanks_GroupsThemByBankWithTheLargestFirst()
    {
        // Arrange
        using CultureScope culture = new();
        Accounts(Account(50m, "Savings", "Bank one"), Account(900m, "Joint", "Bank two"),
            Account(100m, "Current", "Bank one"));

        // Act
        IRenderedComponent<YourAccounts> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-provider").Count == 2);
        using (Assert.Multiple())
        {
            await Assert.That(Headers(cut)).IsEquivalentTo(["Bank two", "Bank one"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(cut.FindAll(".ft-provider")[1].TextContent).Contains("2 accounts · £150.00");
            await Assert.That(AccountsUnder(cut, 1)).IsEquivalentTo(["Current", "Savings"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task Collapse_HidesTheBanksAccountsAndKeepsThemHiddenNextTime()
    {
        // Arrange
        Accounts(Account(100m, "Current", "Bank one"), Account(900m, "Joint", "Bank two"));
        IRenderedComponent<YourAccounts> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-provider").Count == 2);

        // Act
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Hide Bank one accounts']").Click());
        await DisposeComponentsAsync();
        IRenderedComponent<YourAccounts> again = RenderPage();
        again.WaitForState(() => again.FindAll(".ft-provider").Count == 2);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(again.Find("button[aria-label='Show Bank one accounts']").GetAttribute("aria-expanded"))
                .IsEqualTo("false");
            await Assert.That(AccountsUnder(again, 1)).IsEmpty();
            await Assert.That(AccountsUnder(again, 0)).IsEquivalentTo(["Joint"]);
        }
    }

    private void Accounts(params AccountAndTransactions[] accounts) =>
        _accountService.GetAccountsAndMostRecentTransactionsAsync(5, Arg.Any<CancellationToken>())
            .Returns(_ => accounts.ToAsyncEnumerable());

    private IRenderedComponent<YourAccounts> RenderPage() =>
        Render<YourAccounts>(p => p.AddCascadingValue(_applicationState));

    private static List<string> Headers(IRenderedComponent<YourAccounts> cut) =>
        cut.FindAll(".ft-provider__name .mud-typography-subtitle1").Select(static x => x.TextContent.Trim()).ToList();

    /// <summary>
    /// The account names shown under the <paramref name="index"/>th bank.
    /// </summary>
    private static List<string> AccountsUnder(IRenderedComponent<YourAccounts> cut, int index) =>
        cut.FindAll(".ft-provider")[index].QuerySelectorAll(".ft-account-heading .mud-typography-subtitle1")
            .Select(static x => x.TextContent.Trim()).ToList();

    private static AccountAndTransactions Account(decimal balance, string name = "Current",
        string provider = "Bank one") => new()
    {
        AccountId = Guid.NewGuid(),
        AccountName = name,
        ProviderName = provider,
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
            ProviderName = "Bank one",
            Logo = [],
            AccountType = accountType,
            AccountBalance = 0m,
            AvailableBalance = 0m,
            CardEnding = cardEnding
        };

        // Act
        string caption = AccountCard.TypeCaption(account);

        // Assert
        await Assert.That(caption).IsEqualTo(expected);
    }
}
