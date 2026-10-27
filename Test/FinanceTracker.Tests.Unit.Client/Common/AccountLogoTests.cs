using Bunit;
using FinanceTracker.Client.Components.Common;

namespace FinanceTracker.Tests.Unit.Client.Common;

public class AccountLogoTests : ComponentTestBase
{
    [Test]
    public async Task Render_WithSvgBytes_EmbedsThemAsADataUri()
    {
        // Arrange
        byte[] logo = "<svg/>"u8.ToArray();

        // Act
        IRenderedComponent<AccountLogo> cut = Render<AccountLogo>(p => p.Add(x => x.Logo, logo));

        // Assert
        await Assert.That(cut.Find("img").GetAttribute("src"))
            .IsEqualTo($"data:image/svg+xml;base64,{Convert.ToBase64String(logo)}");
    }

    [Test]
    public async Task Render_WithAUrl_PrefersItToTheBytes()
    {
        // Act
        IRenderedComponent<AccountLogo> cut = Render<AccountLogo>(p => p
            .Add(x => x.Logo, "<svg/>"u8.ToArray())
            .Add(x => x.Src, "https://example.com/bank.svg"));

        // Assert
        await Assert.That(cut.Find("img").GetAttribute("src")).IsEqualTo("https://example.com/bank.svg");
    }
}
