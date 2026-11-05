using Bunit;
using FinanceTracker.Client.Components.Common;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Common;

public class MoneyTextTests : ComponentTestBase
{
    [Test]
    public async Task Render_FormatsTheAmountInTheAppsCulture()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<MoneyText> cut = Render<MoneyText>(p => p.Add(x => x.Value, -1234.5m));

        // Assert
        await Assert.That(cut.Find(".ft-money").TextContent).IsEqualTo("-£1,234.50");
    }

    [Test]
    [Arguments(-1, "ft-money--out")]
    [Arguments(1, "ft-money--in")]
    public async Task Render_Signed_ColoursByTheSignInTheMoneyColours(decimal value, string expected)
    {
        // Act
        IRenderedComponent<MoneyText> cut = Render<MoneyText>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.Signed, true));

        // Assert
        await Assert.That(cut.Find(".ft-money").ClassList).Contains(expected);
    }

    [Test]
    public async Task Render_SignedZero_IsUncoloured()
    {
        // Act
        IRenderedComponent<MoneyText> cut = Render<MoneyText>(p => p
            .Add(x => x.Value, 0m)
            .Add(x => x.Signed, true));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".ft-money").ClassList).DoesNotContain("ft-money--in");
            await Assert.That(cut.Find(".ft-money").ClassList).DoesNotContain("ft-money--out");
        }
    }

    [Test]
    public async Task Render_NotSigned_LeavesNegativeAmountsUncoloured()
    {
        // Act
        IRenderedComponent<MoneyText> cut = Render<MoneyText>(p => p.Add(x => x.Value, -1m));

        // Assert
        await Assert.That(cut.Find(".ft-money").ClassList).DoesNotContain("ft-money--out");
    }

    [Test]
    public async Task Render_WithAColour_OverridesTheSign()
    {
        // Act
        IRenderedComponent<MoneyText> cut = Render<MoneyText>(p => p
            .Add(x => x.Value, -1m)
            .Add(x => x.Signed, true)
            .Add(x => x.Color, Color.Primary));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindComponent<MudText>().Instance.Color).IsEqualTo(Color.Primary);
            await Assert.That(cut.Find(".ft-money").ClassList).DoesNotContain("ft-money--out");
        }
    }
}
