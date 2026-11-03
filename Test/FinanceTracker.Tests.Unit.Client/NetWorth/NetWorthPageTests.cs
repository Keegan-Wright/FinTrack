using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NetWorthPage = FinanceTracker.Client.Pages.NetWorth;

namespace FinanceTracker.Tests.Unit.Client.NetWorth;

public class NetWorthPageTests : ComponentTestBase
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly INetWorthService _netWorthService = Substitute.For<INetWorthService>();
    private readonly ApplicationState _applicationState = new();

    public NetWorthPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_netWorthService);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
    }

    [Test]
    public async Task Render_ShowsTodaysFigureHowItsChangedAndWhatMakesItUp()
    {
        // Arrange
        using CultureScope culture = new();
        _netWorthService.GetNetWorthAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>()).Returns(History());

        // Act
        IRenderedComponent<NetWorthPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("£1,460.00");
            await Assert.That(cut.Markup).Contains("Up £200.00 this month");
            await Assert.That(cut.Markup).Contains("No history from before this year");
            await Assert.That(cut.Markup).Contains("Car loan");
            await Assert.That(cut.Markup).Contains("is worked out from your transactions");
        }
    }

    [Test]
    public async Task Render_NoHistory_SaysItBuildsUpFromTheFirstSync()
    {
        // Arrange
        _netWorthService.GetNetWorthAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new NetWorthHistory());

        // Act
        IRenderedComponent<NetWorthPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Markup).Contains("starts building up from your first sync");
    }

    [Test]
    public async Task Render_LoadsTheLastSixMonths()
    {
        // Arrange
        _netWorthService.GetNetWorthAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>()).Returns(History());

        // Act
        RenderPage();

        // Assert
        await _netWorthService.Received(1).GetNetWorthAsync(new DateOnly(2026, 5, 2), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ChoosingAll_LoadsEverything()
    {
        // Arrange
        _netWorthService.GetNetWorthAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>()).Returns(History());
        IRenderedComponent<NetWorthPage> cut = RenderPage();

        // Act
        await cut.FindAll("button").Single(x => x.TextContent.Trim() == "All").ClickAsync(new());

        // Assert
        await _netWorthService.Received(1).GetNetWorthAsync(null, Arg.Any<CancellationToken>());
    }

    private static NetWorthHistory History() => new()
    {
        Points =
        [
            new NetWorthPoint { Date = new DateOnly(2026, 10, 31), NetWorth = 1260m, IsDerived = true },
            new NetWorthPoint
            {
                Date = new DateOnly(2026, 11, 1), Assets = 2400m, Liabilities = 940m, NetWorth = 1460m
            }
        ],
        ChangeThisMonth = 200m,
        Breakdown =
        [
            new NetWorthItem { Name = "Current", Kind = NetWorthItemKind.Account, Amount = 2400m },
            new NetWorthItem { Name = "Car loan", Kind = NetWorthItemKind.Debt, Amount = -900m },
            new NetWorthItem { Name = "Card", Kind = NetWorthItemKind.Card, Amount = -40m }
        ]
    };

    private IRenderedComponent<NetWorthPage> RenderPage()
    {
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
        return Render<NetWorthPage>(p => p.AddCascadingValue(_applicationState));
    }
}
