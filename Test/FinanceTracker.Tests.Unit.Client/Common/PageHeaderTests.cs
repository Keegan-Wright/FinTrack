using AngleSharp.Dom;
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

    [Test]
    public async Task Render_WithActionsAndFileActions_DividesThemActionsFirst()
    {
        // Act
        IRenderedComponent<PageHeader> cut = Render<PageHeader>(p => p
            .Add(x => x.Title, "Reports")
            .Add(x => x.Actions, "<button id=\"breakdown\">By tag</button>")
            .Add(x => x.FileActions, "<button id=\"export\">Export PDF</button>"));

        // Assert
        IElement[] children = cut.Find(".ft-page-header__actions").Children.ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(children).Count().IsEqualTo(3);
            await Assert.That(children[0].Id).IsEqualTo("breakdown");
            await Assert.That(children[1].ClassList).Contains("ft-page-header__divider");
            await Assert.That(children[2].Id).IsEqualTo("export");
        }
    }

    [Test]
    public async Task Render_FileActionsOnly_HasNoDivider()
    {
        // Act
        IRenderedComponent<PageHeader> cut = Render<PageHeader>(p => p
            .Add(x => x.Title, "Transactions")
            .Add(x => x.FileActions, "<button id=\"export\">Export CSV</button>"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-page-header__actions #export")).HasSingleItem();
            await Assert.That(cut.FindAll(".ft-page-header__divider")).IsEmpty();
        }
    }
}
