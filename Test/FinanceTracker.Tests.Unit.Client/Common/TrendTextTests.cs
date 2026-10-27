using Bunit;
using FinanceTracker.Client.Components.Common;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Common;

public class TrendTextTests : ComponentTestBase
{
    [Test]
    public async Task Render_WithNothingToCompare_SaysSo()
    {
        // Act
        IRenderedComponent<TrendText> cut = Render<TrendText>(p => p.Add(x => x.Value, null));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("No previous period to compare");
            await Assert.That(cut.FindComponents<MudIcon>()).IsEmpty();
        }
    }

    [Test]
    [Arguments(0.12, true, Color.Success)]
    [Arguments(0.12, false, Color.Error)]
    [Arguments(-0.12, true, Color.Error)]
    [Arguments(-0.12, false, Color.Success)]
    [Arguments(0, true, Color.Default)]
    public async Task Render_ColoursByWhetherTheChangeIsGood(decimal value, bool higherIsBetter, Color expected)
    {
        // Act
        IRenderedComponent<TrendText> cut = Render<TrendText>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.HigherIsBetter, higherIsBetter));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindComponent<MudIcon>().Instance.Color).IsEqualTo(expected);
            await Assert.That(cut.FindComponent<MudText>().Instance.Color).IsEqualTo(expected);
        }
    }

    [Test]
    [Arguments(0.12, Icons.Material.Filled.ArrowUpward)]
    [Arguments(-0.12, Icons.Material.Filled.ArrowDownward)]
    [Arguments(0, Icons.Material.Filled.Remove)]
    public async Task Render_PointsTheArrowWithTheChange(decimal value, string expected)
    {
        // Act
        IRenderedComponent<TrendText> cut = Render<TrendText>(p => p.Add(x => x.Value, value));

        // Assert
        await Assert.That(cut.FindComponent<MudIcon>().Instance.Icon).IsEqualTo(expected);
    }

    [Test]
    public async Task Render_ShowsTheSizeOfTheChangeWithoutItsSign()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<TrendText> cut = Render<TrendText>(p => p.Add(x => x.Value, -0.123m));

        // Assert
        await Assert.That(cut.Find(".ft-money").TextContent).IsEqualTo("12%");
    }
}
