using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Client.Rules;

public class RuleEditorTests : ComponentTestBase
{
    private static readonly Guid TagId = Guid.NewGuid();

    private readonly IRulesService _rulesService = Substitute.For<IRulesService>();
    private readonly ApplicationState _applicationState = new();

    private readonly Rule _groceries = new()
    {
        Id = Guid.NewGuid(),
        Name = "Groceries",
        IsEnabled = true,
        MatchMode = RuleMatchMode.All,
        Conditions = [new RuleCondition { Field = RuleField.Counterparty, Operator = RuleOperator.Contains,
            Value = "grocer" }],
        Actions = [new RuleAction { Type = RuleActionType.AddTag, ClassificationId = TagId }]
    };

    public RuleEditorTests()
    {
        AddBunitPersistentComponentState();
        Services.AddScoped<IValidator<SaveRule>, SaveRuleValidator>();
        Services.AddScoped<IValidator<RuleCondition>, RuleConditionValidator>();

        ITransactionsService transactionsService = Substitute.For<ITransactionsService>();
        transactionsService.GetAccountsForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<TransactionAccountFilter>());
        IClassificationService classificationService = Substitute.For<IClassificationService>();
        classificationService.GetAllCustomClassificationsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new[] { new Classification { ClassificationId = TagId, Tag = "Shopping" } }
                .ToAsyncEnumerable());

        Services.AddSingleton(_rulesService);
        Services.AddSingleton(transactionsService);
        Services.AddSingleton(classificationService);

        _rulesService.GetRuleAsync(_groceries.Id, Arg.Any<CancellationToken>()).Returns(_groceries);
    }

    [Test]
    public async Task Save_EditedRule_UpdatesItAndGoesBackToTheList()
    {
        // Arrange
        Render<MudPopoverProvider>();
        IRenderedComponent<RuleEditor> cut = RenderEditor(_groceries.Id);

        // Act
        await Button(cut, "Save").ClickAsync(new MouseEventArgs());

        // Assert
        using (Assert.Multiple())
        {
            await _rulesService.Received(1).UpdateRuleAsync(_groceries.Id,
                Arg.Is<SaveRule>(x => x.Name == "Groceries" && x.Actions.Single().ClassificationId == TagId),
                Arg.Any<CancellationToken>());
            await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/Rules");
        }
    }

    [Test]
    public async Task Save_NewRuleLeftBlank_DoesntSave()
    {
        // Arrange
        Render<MudPopoverProvider>();
        IRenderedComponent<RuleEditor> cut = RenderEditor(null);

        // Act
        Button(cut, "Save").Click();

        // Assert
        using (Assert.Multiple())
        {
            await _rulesService.DidNotReceiveWithAnyArgs().AddRuleAsync(default!, default);
            await Assert.That(cut.Markup).Contains("Name is required");
        }
    }

    [Test]
    public async Task RuleThatDoesntExist_SaysSo()
    {
        // Arrange
        Guid missing = Guid.NewGuid();
        _rulesService.GetRuleAsync(missing, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Not found"));

        // Act
        IRenderedComponent<RuleEditor> cut = RenderEditor(missing);

        // Assert
        await Assert.That(cut.Markup).Contains("That rule doesn't exist any more");
    }

    [Test]
    public async Task Delete_OnceConfirmed_DeletesItAndGoesBackToTheList()
    {
        // Arrange
        Render<MudPopoverProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<RuleEditor> cut = RenderEditor(_groceries.Id);

        // Act
        Button(cut, "Delete").Click();
        dialogs.WaitForElements("button").Single(x => x.TextContent.Trim() == "Delete").Click();

        // Assert
        using (Assert.Multiple())
        {
            await _rulesService.Received(1).DeleteRuleAsync(_groceries.Id, Arg.Any<CancellationToken>());
            await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/Rules");
        }
    }

    private IRenderedComponent<RuleEditor> RenderEditor(Guid? id) => Render<RuleEditor>(p => p
        .Add(x => x.Id, id)
        .AddCascadingValue(_applicationState));

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<RuleEditor> cut, string text) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
