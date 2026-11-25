using System.Text.Json;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace FinanceTracker.Tests.Unit.Ai;

public class NetWorthToolsTests : AiToolTestBase
{
    private readonly INetWorthService _netWorthService = Substitute.For<INetWorthService>();

    public NetWorthToolsTests()
    {
        Services.AddSingleton(_netWorthService);
    }

    [Test]
    public async Task GetNetWorth_KeepsTheLastPointOfEachMonth()
    {
        // Arrange
        Returns([
            Point(new DateOnly(2026, 4, 30), 1000m),
            Point(new DateOnly(2026, 5, 1), 1100m),
            Point(new DateOnly(2026, 5, 31), 1200m),
            Point(new DateOnly(2026, 6, 14), 1300m)
        ]);

        // Act
        JsonElement netWorth = await CallAsync("get_net_worth");

        // Assert
        List<decimal> months = netWorth.GetProperty("months").EnumerateArray()
            .Select(x => x.GetProperty("netWorth").GetDecimal())
            .ToList();
        await Assert.That(months).IsEquivalentTo([1000m, 1200m, 1300m], CollectionOrdering.Matching);
        await Assert.That(netWorth.GetProperty("now").GetProperty("netWorth").GetDecimal()).IsEqualTo(1300m);
    }

    [Test]
    public async Task GetNetWorth_KeepsTheLastTwoYearsOfMonths()
    {
        // Arrange
        DateOnly start = new(2023, 1, 31);
        Returns(Enumerable.Range(0, 30).Select(x => Point(start.AddMonths(x), x)).ToList());

        // Act
        JsonElement netWorth = await CallAsync("get_net_worth");

        // Assert
        await Assert.That(netWorth.GetProperty("months").GetArrayLength()).IsEqualTo(24);
    }

    [Test]
    public async Task GetNetWorth_WithoutHistory_HasNoCurrentValue()
    {
        // Arrange
        Returns([]);

        // Act
        JsonElement netWorth = await CallAsync("get_net_worth");

        // Assert, nulls are left out of tool results
        await Assert.That(netWorth.TryGetProperty("now", out _)).IsFalse();
    }

    private void Returns(List<NetWorthPoint> points) =>
        _netWorthService.GetNetWorthAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new NetWorthHistory { Points = points, Breakdown = [] });

    private static NetWorthPoint Point(DateOnly date, decimal netWorth) =>
        new() { Date = date, Assets = netWorth, Liabilities = 0, NetWorth = netWorth };
}
