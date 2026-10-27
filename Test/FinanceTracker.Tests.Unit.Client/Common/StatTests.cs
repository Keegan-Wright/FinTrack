using Bunit;
using FinanceTracker.Client.Components.Common;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Common;

public class StatTests : ComponentTestBase
{
    [Test]
    public async Task Render_ShowsTheLabelAndAmount()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<Stat> cut = Render<Stat>(p => p
            .Add(x => x.Label, "Money in")
            .Add(x => x.Value, 2500m));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".ft-stat__label").TextContent).IsEqualTo("Money in");
            await Assert.That(cut.Find(".ft-money").TextContent).IsEqualTo("£2,500.00");
            await Assert.That(cut.FindAll(".ft-stat__hint")).IsEmpty();
        }
    }

    [Test]
    public async Task Render_PassesSignAndColourToTheAmount()
    {
        // Act
        IRenderedComponent<Stat> cut = Render<Stat>(p => p
            .Add(x => x.Label, "Net")
            .Add(x => x.Value, -1m)
            .Add(x => x.Signed, true)
            .Add(x => x.Color, Color.Info));

        // Assert
        MoneyText money = cut.FindComponent<MoneyText>().Instance;

        using (Assert.Multiple())
        {
            await Assert.That(money.Signed).IsTrue();
            await Assert.That(money.Color).IsEqualTo(Color.Info);
        }
    }

    [Test]
    public async Task Render_WithContext_ShowsItUnderTheAmount()
    {
        // Act
        IRenderedComponent<Stat> cut = Render<Stat>(p => p
            .Add(x => x.Label, "Money in")
            .Add(x => x.Value, 1m)
            .AddChildContent("vs last month"));

        // Assert
        await Assert.That(cut.Find(".ft-stat__hint").TextContent).IsEqualTo("vs last month");
    }
}
