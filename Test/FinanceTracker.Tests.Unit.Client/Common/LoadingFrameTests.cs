using Bunit;
using FinanceTracker.Client.Components.Common;

namespace FinanceTracker.Tests.Unit.Client.Common;

public class LoadingFrameTests : ComponentTestBase
{
    [Test]
    public async Task Render_Loading_ShowsTheMessageOverTheContent()
    {
        // Act
        IRenderedComponent<LoadingFrame> cut = Render<LoadingFrame>(p => p
            .Add(x => x.Loading, true)
            .Add(x => x.Message, "Fetching transactions")
            .AddChildContent("<p id=\"content\">Ready</p>"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find("[role=status]").TextContent).Contains("Fetching transactions");
            await Assert.That(cut.FindAll("#content")).HasSingleItem();
        }
    }

    [Test]
    public async Task Render_Loaded_ShowsOnlyTheContent()
    {
        // Act
        IRenderedComponent<LoadingFrame> cut = Render<LoadingFrame>(p => p
            .Add(x => x.Loading, false)
            .AddChildContent("<p id=\"content\">Ready</p>"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll("[role=status]")).IsEmpty();
            await Assert.That(cut.FindAll("#content")).HasSingleItem();
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Render_Cover_HidesTheContentUnderTheSpinner(bool cover)
    {
        // Act
        IRenderedComponent<LoadingFrame> cut = Render<LoadingFrame>(p => p
            .Add(x => x.Loading, true)
            .Add(x => x.Cover, cover));

        // Assert
        await Assert.That(cut.Find("[role=status]").ClassList.Contains("ft-loading-frame__overlay--cover"))
            .IsEqualTo(cover);
    }

    [Test]
    public async Task Render_KeepsTheContentsHeight()
    {
        // Act
        IRenderedComponent<LoadingFrame> cut = Render<LoadingFrame>(p => p
            .Add(x => x.Loading, true)
            .Add(x => x.MinHeight, 320));

        // Assert
        await Assert.That(cut.Find(".ft-loading-frame").GetAttribute("style")).IsEqualTo("min-height: 320px");
    }
}
