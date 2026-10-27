using Bunit;
using FinanceTracker.Client.Components.Common;

namespace FinanceTracker.Tests.Unit.Client.Common;

public class SectionTests : ComponentTestBase
{
    [Test]
    public async Task Render_WithoutTitleOrActions_HasNoHeader()
    {
        // Act
        IRenderedComponent<Section> cut = Render<Section>(p => p.AddChildContent("<p>Body</p>"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-section__header")).IsEmpty();
            await Assert.That(cut.Find(".ft-section__body").TextContent).IsEqualTo("Body");
        }
    }

    [Test]
    public async Task Render_WithTitleAndCaption_ShowsBoth()
    {
        // Act
        IRenderedComponent<Section> cut = Render<Section>(p => p
            .Add(x => x.Title, "Top categories")
            .Add(x => x.Caption, "This month"));

        // Assert
        string heading = cut.Find(".ft-section__heading").TextContent;

        using (Assert.Multiple())
        {
            await Assert.That(heading).Contains("Top categories");
            await Assert.That(heading).Contains("This month");
        }
    }

    [Test]
    public async Task Render_WithTitleContent_ShowsItInPlaceOfTheTitle()
    {
        // Act
        IRenderedComponent<Section> cut = Render<Section>(p => p
            .Add(x => x.Title, "Plain title")
            .Add(x => x.TitleContent, "<strong id=\"rich\">Rich title</strong>"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll("#rich")).HasSingleItem();
            await Assert.That(cut.Markup).DoesNotContain("Plain title");
        }
    }

    [Test]
    public async Task Render_WithOnlyActions_ShowsTheHeader()
    {
        // Act
        IRenderedComponent<Section> cut = Render<Section>(p => p
            .Add(x => x.Actions, "<button id=\"action\">Add</button>"));

        // Assert
        await Assert.That(cut.FindAll(".ft-section__header #action")).HasSingleItem();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Render_ScrollBody_MarksTheSectionToScroll(bool scrollBody)
    {
        // Act
        IRenderedComponent<Section> cut = Render<Section>(p => p.Add(x => x.ScrollBody, scrollBody));

        // Assert
        await Assert.That(cut.Find(".ft-section").ClassList.Contains("ft-section--scroll")).IsEqualTo(scrollBody);
    }
}
