using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Rules;
using FinanceTracker.Contracts.Rules;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Rules;

public class RuleListTests : ComponentTestBase
{
    private static readonly Dictionary<Guid, string> NoNames = [];

    private readonly List<Rule> _rules = [Rule("First"), Rule("Second"), Rule("Third")];
    private IReadOnlyList<Rule>? _reordered;
    private Rule? _toggled;

    [Test]
    public async Task Render_ShowsTheRulesNumberedInOrder()
    {
        // Act
        IRenderedComponent<RuleList> cut = RenderList();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-rule__name").Select(static x => x.TextContent))
                .IsEquivalentTo(["First", "Second", "Third"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(cut.FindAll(".ft-rule__position").Select(static x => x.TextContent))
                .IsEquivalentTo(["1", "2", "3"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task MoveUp_PutsTheRuleOnePlaceEarlier()
    {
        // Arrange
        IRenderedComponent<RuleList> cut = RenderList();

        // Act
        Button(cut, "Move Third up").Click();

        // Assert
        await Assert.That(Names(_reordered)).IsEquivalentTo(["First", "Third", "Second"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task MoveButtons_AtTheEnds_AreDisabled()
    {
        // Act
        IRenderedComponent<RuleList> cut = RenderList();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Button(cut, "Move First up").HasAttribute("disabled")).IsTrue();
            await Assert.That(Button(cut, "Move Third down").HasAttribute("disabled")).IsTrue();
            await Assert.That(Button(cut, "Move Second up").HasAttribute("disabled")).IsFalse();
        }
    }

    [Test]
    public async Task Drop_PutsTheRuleWhereItLanded()
    {
        // Arrange
        IRenderedComponent<RuleList> cut = RenderList();

        // Act, dragging the last to the top is one move rather than two clicks
        await cut.InvokeAsync(() => cut.Instance.OnDroppedAsync(new MudItemDropInfo<Rule>(_rules[2], "rules", 0)));

        // Assert
        await Assert.That(Names(_reordered)).IsEquivalentTo(["Third", "First", "Second"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Drop_WhereItWas_ChangesNothing()
    {
        // Arrange
        IRenderedComponent<RuleList> cut = RenderList();

        // Act
        await cut.InvokeAsync(() => cut.Instance.OnDroppedAsync(new MudItemDropInfo<Rule>(_rules[1], "rules", 1)));

        // Assert
        await Assert.That(_reordered).IsNull();
    }

    [Test]
    public async Task Switch_AsksToToggleThatRule()
    {
        // Arrange
        IRenderedComponent<RuleList> cut = RenderList();

        // Act
        cut.FindAll("input[type=checkbox]")[1].Change(false);

        // Assert
        await Assert.That(_toggled?.Name).IsEqualTo("Second");
    }

    [Test]
    public async Task RuleWithNoActions_ShowsWhyAndCantBeSwitchedOn()
    {
        // Arrange
        _rules[0] = Rule("First", enabled: false, actions: []);

        // Act
        IRenderedComponent<RuleList> cut = RenderList();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Its tag was deleted, give it an action to turn it back on");
            await Assert.That(cut.FindAll("input[type=checkbox]")[0].HasAttribute("disabled")).IsTrue();
        }
    }

    private IRenderedComponent<RuleList> RenderList() => Render<RuleList>(parameters => parameters
        .Add(x => x.Rules, _rules)
        .Add(x => x.AccountNames, NoNames)
        .Add(x => x.TagNames, NoNames)
        .Add(x => x.OnReorder, (IReadOnlyList<Rule> reordered) => _reordered = reordered)
        .Add(x => x.OnToggle, (Rule rule) => _toggled = rule));

    private static IElement Button(IRenderedComponent<RuleList> cut, string label) =>
        cut.FindAll("button").Single(x => x.GetAttribute("aria-label") == label);

    private static IEnumerable<string>? Names(IReadOnlyList<Rule>? rules) => rules?.Select(static x => x.Name);

    private static Rule Rule(string name, bool enabled = true, List<RuleAction>? actions = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsEnabled = enabled,
        Conditions = [new RuleCondition { Field = RuleField.Description, Operator = RuleOperator.Contains,
            Value = name.ToUpperInvariant() }],
        Actions = actions ?? [new RuleAction { Type = RuleActionType.MarkNotATransfer }]
    };
}
