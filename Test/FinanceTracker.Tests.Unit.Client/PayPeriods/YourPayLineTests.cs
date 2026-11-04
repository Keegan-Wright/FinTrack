using Bunit;
using FinanceTracker.Client.Components.PayPeriods;
using FinanceTracker.Contracts.PayPeriods;

namespace FinanceTracker.Tests.Unit.Client.PayPeriods;

public class YourPayLineTests : ComponentTestBase
{
    [Test]
    public async Task Render_NoPayYet_SaysWhenItWillAndOffersNothing()
    {
        // Act
        IRenderedComponent<YourPayLine> cut = Render(YourPays.NoneYet(2026, 11));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Once your pay has come in three months running");
            await Assert.That(cut.FindAll("button")).IsEmpty();
        }
    }

    [Test]
    public async Task Render_FromPayToPay_SaysWhoPaysAndWhen()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<YourPayLine> cut = Render(YourPays.FromPayToPay());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup)
                .Contains("From pay to pay: ACME LTD · £2,400.00 into Main on the last working day of the month.");
            await Assert.That(Buttons(cut)).IsEquivalentTo(["Choose another income", "Use calendar months"]);
        }
    }

    [Test]
    [Arguments(false, "Use calendar months", false)]
    [Arguments(true, "Use pay periods", true)]
    public async Task Click_Switching_RaisesWhichToUse(bool off, string button, bool expected)
    {
        // Arrange
        bool? chosen = null;
        IRenderedComponent<YourPayLine> cut = Render<YourPayLine>(p => p
            .Add(x => x.Pay, YourPays.FromPayToPay(off))
            .Add(x => x.OnUsePayPeriods, (bool payPeriods) => chosen = payPeriods));

        // Act
        await cut.InvokeAsync(() => cut.FindAll("button").Single(x => x.TextContent.Trim() == button).Click());

        // Assert
        await Assert.That(chosen).IsEqualTo(expected);
    }

    private IRenderedComponent<YourPayLine> Render(YourPay pay) => Render<YourPayLine>(p => p.Add(x => x.Pay, pay));

    private static List<string> Buttons(IRenderedComponent<YourPayLine> cut) =>
        cut.FindAll("button").Select(static x => x.TextContent.Trim()).ToList();
}
