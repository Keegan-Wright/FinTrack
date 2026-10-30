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
        _rulesService.UpdateRuleAsync(_groceries.Id, Arg.Any<SaveRule>(), Arg.Any<CancellationToken>())
            .Returns(_groceries);
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

    [Test]
    public async Task Preview_ShowsWhatWouldChange()
    {
        // Arrange
        Render<MudPopoverProvider>();
        _rulesService.PreviewRuleAsync(_groceries.Id, Arg.Any<SaveRule>(), Arg.Any<CancellationToken>())
            .Returns(new RulePreview
            {
                Changed = 1,
                Changes =
                [
                    new RulePreviewChange { Description = "GROCER STORE 1", Amount = -12.5m, TagsAdded = ["Shopping"] }
                ]
            });
        IRenderedComponent<RuleEditor> cut = RenderEditor(_groceries.Id);

        // Act
        await Button(cut, "Preview").ClickAsync(new MouseEventArgs());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Applying it would change 1 transaction.");
            await Assert.That(cut.Markup).Contains("GROCER STORE 1");
            await Assert.That(cut.Markup).Contains("+ Shopping");
        }
    }

    [Test]
    public async Task SaveAndApply_OnceConfirmed_SavesThenAppliesIt()
    {
        // Arrange
        Render<MudPopoverProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        _rulesService.ApplyRuleToExistingAsync(_groceries.Id, Arg.Any<CancellationToken>()).Returns(3);
        IRenderedComponent<RuleEditor> cut = RenderEditor(_groceries.Id);

        // Act
        Button(cut, "Save and apply").Click();
        await dialogs.WaitForElements("button").Single(x => x.TextContent.Trim() == "Save and apply")
            .ClickAsync(new MouseEventArgs());

        // Assert
        using (Assert.Multiple())
        {
            Received.InOrder(() =>
            {
                _rulesService.UpdateRuleAsync(_groceries.Id, Arg.Any<SaveRule>(), Arg.Any<CancellationToken>());
                _rulesService.ApplyRuleToExistingAsync(_groceries.Id, Arg.Any<CancellationToken>());
            });
            await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/Rules");
        }
    }

    [Test]
    public async Task NewFromATransaction_StartsFromItsDraft()
    {
        // Arrange
        Render<MudPopoverProvider>();
        Guid transactionId = Guid.NewGuid();
        _rulesService.GetDraftFromTransactionAsync(transactionId, Arg.Any<CancellationToken>())
            .Returns(new SaveRule
            {
                Name = "Game Store",
                Conditions = [new RuleCondition { Field = RuleField.Counterparty, Operator = RuleOperator.Contains,
                    Value = "Game Store" }],
                Actions = [new RuleAction { Type = RuleActionType.AddTag, ClassificationId = TagId }]
            });
        Services.GetRequiredService<NavigationManager>().NavigateTo($"Rules/new?from={transactionId}");

        // Act
        IRenderedComponent<RuleEditor> cut = RenderEditor(null);

        // Assert
        await Assert.That(cut.FindAll("input").Any(x => x.GetAttribute("value") == "Game Store")).IsTrue();
    }

    private IRenderedComponent<RuleEditor> RenderEditor(Guid? id) => Render<RuleEditor>(p => p
        .Add(x => x.Id, id)
        .AddCascadingValue(_applicationState));

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<RuleEditor> cut, string text) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
