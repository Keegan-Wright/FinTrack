using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RulesPage = FinanceTracker.Client.Pages.Rules;

namespace FinanceTracker.Tests.Unit.Client.Rules;

public class RulesPageTests : ComponentTestBase
{
    private readonly IRulesService _rulesService = Substitute.For<IRulesService>();
    private readonly ApplicationState _applicationState = new();
    private readonly List<Rule> _rules = [Rule("First"), Rule("Second")];

    public RulesPageTests()
    {
        AddBunitPersistentComponentState();
        ITransactionsService transactionsService = Substitute.For<ITransactionsService>();
        transactionsService.GetAccountsForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<TransactionAccountFilter>());
        IClassificationService classificationService = Substitute.For<IClassificationService>();
        classificationService.GetAllCustomClassificationsAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Classification>());

        Services.AddSingleton(_rulesService);
        Services.AddSingleton(transactionsService);
        Services.AddSingleton(classificationService);

        _rulesService.GetRulesAsync(Arg.Any<CancellationToken>()).Returns(_ => _rules.ToAsyncEnumerable());
    }

    [Test]
    public async Task Reorder_Saves_TheRulesInTheirNewOrder()
    {
        // Arrange
        IRenderedComponent<RulesPage> cut = RenderPage();

        // Act
        cut.Find("button[aria-label='Move Second up']").Click();

        // Assert
        using (Assert.Multiple())
        {
            await _rulesService.Received(1).ReorderRulesAsync(
                Arg.Is<ReorderRules>(x => x.RuleIds.SequenceEqual(new[] { _rules[1].Id, _rules[0].Id })),
                Arg.Any<CancellationToken>());
            await Assert.That(Names(cut)).IsEquivalentTo(["Second", "First"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task Reorder_ThatCantBeSaved_PutsTheOrderBack()
    {
        // Arrange
        _rulesService.ReorderRulesAsync(Arg.Any<ReorderRules>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        IRenderedComponent<RulesPage> cut = RenderPage();

        // Act
        cut.Find("button[aria-label='Move Second up']").Click();

        // Assert
        await Assert.That(Names(cut)).IsEquivalentTo(["First", "Second"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Switch_TurnsTheRuleOffKeepingEverythingElse()
    {
        // Arrange
        Rule off = Rule("First", enabled: false);
        _rulesService.UpdateRuleAsync(_rules[0].Id, Arg.Any<SaveRule>(), Arg.Any<CancellationToken>()).Returns(off);
        IRenderedComponent<RulesPage> cut = RenderPage();

        // Act
        cut.FindAll("input[type=checkbox]")[0].Change(false);

        // Assert
        using (Assert.Multiple())
        {
            await _rulesService.Received(1).UpdateRuleAsync(_rules[0].Id,
                Arg.Is<SaveRule>(x => !x.IsEnabled && x.Name == "First" && x.Conditions.Count == 1),
                Arg.Any<CancellationToken>());
            await Assert.That(cut.FindAll("input[type=checkbox]")[0].HasAttribute("checked")).IsFalse();
        }
    }

    [Test]
    public async Task NoRules_SaysHowToStart()
    {
        // Arrange
        _rules.Clear();

        // Act
        IRenderedComponent<RulesPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Markup).Contains("No rules yet");
    }

    private IRenderedComponent<RulesPage> RenderPage() =>
        Render<RulesPage>(p => p.AddCascadingValue(_applicationState));

    private static IEnumerable<string> Names(IRenderedComponent<RulesPage> cut) =>
        cut.FindAll(".ft-rule__name").Select(static x => x.TextContent);

    private static Rule Rule(string name, bool enabled = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsEnabled = enabled,
        Conditions = [new RuleCondition { Field = RuleField.Description, Operator = RuleOperator.Contains,
            Value = name.ToUpperInvariant() }],
        Actions = [new RuleAction { Type = RuleActionType.MarkNotATransfer }]
    };
}
