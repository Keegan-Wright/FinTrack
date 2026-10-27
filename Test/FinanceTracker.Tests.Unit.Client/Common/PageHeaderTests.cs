using Bunit;
using FinanceTracker.Client.Components.Common;

namespace FinanceTracker.Tests.Unit.Client.Common;

public class PageHeaderTests : ComponentTestBase
{
    [Test]
    public async Task Render_TitleOnly_HasNoSubtitleOrActions()
    {
        // Act
        IRenderedComponent<PageHeader> cut = Render<PageHeader>(p => p.Add(x => x.Title, "Transactions"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".ft-page-header__text").TextContent.Trim()).IsEqualTo("Transactions");
            await Assert.That(cut.FindAll(".ft-page-header__actions")).IsEmpty();
        }
    }

    [Test]
    public async Task Render_WithSubtitleActionsAndContent_ShowsEach()
    {
        // Act
        IRenderedComponent<PageHeader> cut = Render<PageHeader>(p => p
            .Add(x => x.Title, "Transactions")
            .Add(x => x.Subtitle, "Everything synced from your banks")
            .Add(x => x.Actions, "<button id=\"sync\">Sync</button>")
            .AddChildContent("<div id=\"filters\"></div>"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".ft-page-header__text").TextContent)
                .Contains("Everything synced from your banks");
            await Assert.That(cut.FindAll(".ft-page-header__actions #sync")).HasSingleItem();
            await Assert.That(cut.FindAll(".ft-page-header > #filters")).HasSingleItem();
        }
    }
}
