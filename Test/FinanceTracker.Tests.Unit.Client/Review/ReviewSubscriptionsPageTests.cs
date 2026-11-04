using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Client.Review;

public class ReviewSubscriptionsPageTests : ComponentTestBase
{
    private readonly IBillsService _billsService = Substitute.For<IBillsService>();
    private readonly ApplicationState _applicationState = new();

    public ReviewSubscriptionsPageTests()
    {
        Services.AddSingleton(_billsService);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTime(2026, 10, 30, 12, 0, 0,
            DateTimeKind.Utc)));
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
    }

    [Test]
    public async Task Confirm_MakesItABillAndTakesItOffTheList()
    {
        // Arrange
        BillSuggestion suggestion = SuggestionRowTests.Suggestion();
        _billsService.GetSuggestionsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { suggestion }.ToAsyncEnumerable());
        _billsService.ConfirmSuggestionAsync(suggestion.Id, Arg.Any<CancellationToken>())
            .Returns(new Bill { Name = "Streamco", Payee = "Streamco", AccountName = "Card" });
        _billsService.GetSuggestionCountAsync(Arg.Any<CancellationToken>()).Returns(0);
        Render<MudBlazor.MudSnackbarProvider>();
        IRenderedComponent<ReviewSubscriptions> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-suggestion").Count > 0);

        // Act
        cut.FindAll("button").Single(x => x.TextContent.Trim() == "It's a bill").Click();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-suggestion").Count == 0);
        using (Assert.Multiple())
        {
            await _billsService.Received(1).ConfirmSuggestionAsync(suggestion.Id, Arg.Any<CancellationToken>());
            await Assert.That(cut.Markup).Contains("All caught up");
            await Assert.That(_applicationState.SubscriptionsToReview).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Confirm_ABillAlreadyCoversIt_SaysWhichAndTakesItOffTheList()
    {
        // Arrange
        BillSuggestion suggestion = SuggestionRowTests.Suggestion();
        _billsService.GetSuggestionsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { suggestion }.ToAsyncEnumerable());
        _billsService.ConfirmSuggestionAsync(suggestion.Id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new AlreadyABillException("Streaming"));
        _billsService.GetSuggestionCountAsync(Arg.Any<CancellationToken>()).Returns(0);
        IRenderedComponent<MudBlazor.MudSnackbarProvider> snackbars = Render<MudBlazor.MudSnackbarProvider>();
        IRenderedComponent<ReviewSubscriptions> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-suggestion").Count > 0);

        // Act
        cut.FindAll("button").Single(x => x.TextContent.Trim() == "It's a bill").Click();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-suggestion").Count == 0);
        await Assert.That(snackbars.Markup).Contains("Your bill Streaming already covers these payments");
    }

    [Test]
    public async Task Dismiss_DismissesIt()
    {
        // Arrange
        BillSuggestion suggestion = SuggestionRowTests.Suggestion();
        _billsService.GetSuggestionsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { suggestion }.ToAsyncEnumerable());
        Render<MudBlazor.MudSnackbarProvider>();
        IRenderedComponent<ReviewSubscriptions> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-suggestion").Count > 0);

        // Act
        cut.FindAll("button").Single(x => x.TextContent.Trim() == "Not a bill").Click();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-suggestion").Count == 0);
        await _billsService.Received(1).DismissSuggestionAsync(suggestion.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Synced_AddsNewSuggestionsAndKeepsAnsweredOnesOff()
    {
        // Arrange
        BillSuggestion answered = SuggestionRowTests.Suggestion("Streamco");
        BillSuggestion waiting = SuggestionRowTests.Suggestion("Gym");
        BillSuggestion added = SuggestionRowTests.Suggestion("Music");
        List<BillSuggestion> suggestions = [answered, waiting];
        _billsService.GetSuggestionsAsync(Arg.Any<CancellationToken>()).Returns(_ => suggestions.ToAsyncEnumerable());
        Render<MudBlazor.MudSnackbarProvider>();
        IRenderedComponent<ReviewSubscriptions> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-suggestion").Count == 2);
        await cut.InvokeAsync(() => cut.FindAll(".ft-suggestion")
            .Single(x => x.TextContent.Contains("Streamco"))
            .QuerySelectorAll("button").Single(x => x.TextContent.Trim() == "Not a bill").Click());
        cut.WaitForState(() => cut.FindAll(".ft-suggestion").Count == 1);

        // A refresh that started before the answer was saved still has it
        suggestions = [answered, waiting, added];

        // Act
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-suggestion")).Count().IsEqualTo(2);
            await Assert.That(cut.Markup).Contains("Gym");
            await Assert.That(cut.Markup).Contains("Music");
            await Assert.That(cut.Markup).DoesNotContain("Streamco");
        }
    }

    private IRenderedComponent<ReviewSubscriptions> RenderPage() =>
        Render<ReviewSubscriptions>(p => p.AddCascadingValue(_applicationState));
}
