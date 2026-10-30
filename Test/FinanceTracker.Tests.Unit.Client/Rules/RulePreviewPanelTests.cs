using Bunit;
using FinanceTracker.Client.Components.Rules;
using FinanceTracker.Contracts.Rules;

namespace FinanceTracker.Tests.Unit.Client.Rules;

public class RulePreviewPanelTests : ComponentTestBase
{
    [Test]
    public async Task Render_RuleOff_CantBeAppliedAndSaysWhy()
    {
        // Act
        IRenderedComponent<RulePreviewPanel> cut = Render<RulePreviewPanel>(p => p.Add(x => x.CanApply, false));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ApplyButton(cut).HasAttribute("disabled")).IsTrue();
            await Assert.That(cut.Markup).Contains("Turn the rule on to apply it.");
        }
    }

    [Test]
    public async Task Render_Preview_SaysHowManyAndWhatsLeftOut()
    {
        // Arrange
        RulePreview preview = new()
        {
            Changed = 60,
            LeftAlone = 2,
            Changes = Enumerable.Range(0, 60)
                .Select(static x => new RulePreviewChange
                {
                    Description = $"GROCER STORE {x}",
                    TagsAdded = ["Shopping"]
                })
                .ToList()
        };

        // Act
        IRenderedComponent<RulePreviewPanel> cut = Render<RulePreviewPanel>(p => p.Add(x => x.Preview, preview));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Applying it would change 60 transactions.");
            await Assert.That(cut.Markup).Contains("2 transactions you tagged yourself keep your tags.");
            await Assert.That(cut.FindAll(".ft-rule-preview__row")).Count().IsEqualTo(RulePreviewPanel.PageSize);
        }
    }

    [Test]
    public async Task Pager_NextPage_ShowsTheNextChanges()
    {
        // Arrange
        RulePreview preview = new()
        {
            Changed = 25,
            Changes = Enumerable.Range(0, 25)
                .Select(static x => new RulePreviewChange { Description = $"GROCER STORE {x}" })
                .ToList()
        };
        IRenderedComponent<RulePreviewPanel> cut = Render<RulePreviewPanel>(p => p.Add(x => x.Preview, preview));

        // Act
        cut.FindAll("button").Single(x => x.GetAttribute("aria-label")?.Contains("Next") == true).Click();

        // Assert
        await Assert.That(cut.FindAll(".ft-rule-preview__row").Select(static x => x.TextContent))
            .All().Satisfy(x => x.Contains("GROCER STORE 2"));
    }

    [Test]
    public async Task Render_NothingToChange_SaysSo()
    {
        // Act
        IRenderedComponent<RulePreviewPanel> cut = Render<RulePreviewPanel>(p => p.Add(x => x.Preview,
            new RulePreview()));

        // Assert
        await Assert.That(cut.Markup).Contains("Applying it wouldn't change any of your transactions.");
    }

    private static AngleSharp.Dom.IElement ApplyButton(IRenderedComponent<RulePreviewPanel> cut) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == "Save and apply");
}
