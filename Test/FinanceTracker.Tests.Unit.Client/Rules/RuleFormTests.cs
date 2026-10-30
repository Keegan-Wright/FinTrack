using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Rules;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Rules;

public class RuleFormTests : ComponentTestBase
{
    private static readonly Guid TagId = Guid.NewGuid();
    private static readonly Dictionary<Guid, string> TagNames = new() { [TagId] = "Shopping" };

    private IRenderedComponent<MudPopoverProvider> _popovers = null!;

    public RuleFormTests()
    {
        Services.AddScoped<IValidator<SaveRule>, SaveRuleValidator>();
        Services.AddScoped<IValidator<RuleCondition>, RuleConditionValidator>();
    }

    [Test]
    public async Task ValidateAsync_NewBlankRule_ShowsWhatsMissing()
    {
        // Arrange
        IRenderedComponent<RuleForm> cut = RenderForm(RuleDrafts.New());

        // Act
        bool valid = await cut.InvokeAsync(() => cut.Instance.ValidateAsync());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(valid).IsFalse();
            await Assert.That(cut.Markup).Contains("Name is required");
            await Assert.That(cut.Markup).Contains("Value is required");
            await Assert.That(cut.Markup).Contains("Choose a tag");
        }
    }

    [Test]
    public async Task ValidateAsync_TwoTransferAnswers_SaysOnlyOneCanDecide()
    {
        // Arrange
        SaveRule rule = Complete();
        rule.Actions =
        [
            new RuleAction { Type = RuleActionType.MarkAsTransfer },
            new RuleAction { Type = RuleActionType.MarkNotATransfer }
        ];
        IRenderedComponent<RuleForm> cut = RenderForm(rule);

        // Act
        bool valid = await cut.InvokeAsync(() => cut.Instance.ValidateAsync());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(valid).IsFalse();
            await Assert.That(cut.Markup).Contains("Only one action can decide whether it's a transfer");
        }
    }

    [Test]
    public async Task ValidateAsync_CompleteRule_IsValid()
    {
        // Arrange
        IRenderedComponent<RuleForm> cut = RenderForm(Complete());

        // Act
        bool valid = await cut.InvokeAsync(() => cut.Instance.ValidateAsync());

        // Assert
        await Assert.That(valid).IsTrue();
    }

    [Test]
    public async Task AddCondition_AddsARow_AndTheOnlyOneCantBeRemoved()
    {
        // Arrange
        SaveRule rule = Complete();
        IRenderedComponent<RuleForm> cut = RenderForm(rule);
        bool removableAlone = !RemoveConditionButtons(cut).Single().HasAttribute("disabled");

        // Act
        cut.FindAll("button").Single(x => x.TextContent.Trim() == "Add condition").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removableAlone).IsFalse();
            await Assert.That(rule.Conditions).Count().IsEqualTo(2);
            await Assert.That(RemoveConditionButtons(cut).All(x => !x.HasAttribute("disabled"))).IsTrue();
        }
    }

    [Test]
    public async Task ChoosingAmount_StartsTheConditionAgainForAnAmount()
    {
        // Arrange
        SaveRule rule = Complete();
        IRenderedComponent<RuleForm> cut = RenderForm(rule);

        // Act
        await ChooseAsync(cut, "Look at", "Amount");

        // Assert
        RuleCondition condition = rule.Conditions.Single();
        using (Assert.Multiple())
        {
            await Assert.That(condition.Field).IsEqualTo(RuleField.Amount);
            await Assert.That(condition.Operator).IsEqualTo(RuleOperator.Equals);
            await Assert.That(condition.Value).IsEmpty();
            await Assert.That(cut.Markup).Contains("e.g. 12.50, in or out");
        }
    }

    [Test]
    public async Task ChoosingATransferAction_DropsTheTag()
    {
        // Arrange
        SaveRule rule = Complete();
        IRenderedComponent<RuleForm> cut = RenderForm(rule);

        // Act
        await ChooseAsync(cut, "Do", "Mark as not a transfer");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(rule.Actions.Single().Type).IsEqualTo(RuleActionType.MarkNotATransfer);
            await Assert.That(rule.Actions.Single().ClassificationId).IsNull();
        }
    }

    [Test]
    public async Task ChoosingHide_AsksWhichBankClassification()
    {
        // Arrange
        SaveRule rule = Complete();
        IRenderedComponent<RuleForm> cut = RenderForm(rule);

        // Act
        await ChooseAsync(cut, "Do", "Hide bank classification");
        bool valid = await cut.InvokeAsync(() => cut.Instance.ValidateAsync());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(rule.Actions.Single().ClassificationId).IsNull();
            await Assert.That(valid).IsFalse();
            await Assert.That(cut.Markup).Contains("Enter the bank classification to hide");
        }
    }

    private IRenderedComponent<RuleForm> RenderForm(SaveRule rule)
    {
        _popovers = Render<MudPopoverProvider>();
        return Render<RuleForm>(parameters => parameters
            .Add(x => x.Model, rule)
            .Add(x => x.AccountNames, new Dictionary<Guid, string>())
            .Add(x => x.TagNames, TagNames));
    }

    /// <summary>
    /// Opens the select labelled <paramref name="label"/> and clicks <paramref name="item"/>, as a user would.
    /// </summary>
    private async Task ChooseAsync(IRenderedComponent<RuleForm> cut, string label, string item)
    {
        IElement select = cut.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label);
        // MudSelect opens as the button goes down, like a native select
        await select.MouseDownAsync(new MouseEventArgs());

        IElement option = _popovers.WaitForElements(".mud-popover-open .mud-list-item")
            .Single(x => x.TextContent.Trim() == item);
        await option.ClickAsync(new MouseEventArgs());
    }

    private static IEnumerable<IElement> RemoveConditionButtons(IRenderedComponent<RuleForm> cut) =>
        cut.FindAll("button").Where(static x => x.GetAttribute("aria-label") == "Remove condition");

    private static SaveRule Complete() => new()
    {
        Name = "Groceries",
        Conditions = [new RuleCondition { Field = RuleField.Counterparty, Operator = RuleOperator.Contains,
            Value = "grocer" }],
        Actions = [new RuleAction { Type = RuleActionType.AddTag, ClassificationId = TagId }]
    };
}
