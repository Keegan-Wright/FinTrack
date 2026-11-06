using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class YourTransactionsPageTests : ComponentTestBase
{
    private readonly ITransactionsService _transactionsService = Substitute.For<ITransactionsService>();
    private readonly IClassificationService _classificationService = Substitute.For<IClassificationService>();
    private readonly IExportsService _exportsService = Substitute.For<IExportsService>();
    private readonly ApplicationState _applicationState = new();
    private readonly List<IBrowserViewportObserver> _viewportObservers = [];

    public YourTransactionsPageTests()
    {
        AddBunitPersistentComponentState();

        IBillsService billsService = Substitute.For<IBillsService>();
        billsService.GetBillsAsync(Arg.Any<CancellationToken>()).Returns(AsyncEnumerable.Empty<Bill>());
        IDebtsService debtsService = Substitute.For<IDebtsService>();
        debtsService.GetDebtsAsync(Arg.Any<CancellationToken>()).Returns(AsyncEnumerable.Empty<Debt>());
        ISavingsGoalsService goalsService = Substitute.For<ISavingsGoalsService>();
        goalsService.GetSavingsGoalsAsync(Arg.Any<CancellationToken>()).Returns(AsyncEnumerable.Empty<SavingsGoal>());

        _transactionsService.GetAccountsForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<TransactionAccountFilter>());
        _transactionsService.GetProvidersForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<TransactionProviderFilter>());
        _transactionsService.GetTypesForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<TransactionTypeFilter>());
        _transactionsService.GetCategoriesForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<TransactionCategoryFilter>());
        _transactionsService.GetTagsForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<TransactionTagFilter>());
        _classificationService.GetAllCustomClassificationsAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Classification>());

        Services.AddSingleton(_transactionsService);
        Services.AddSingleton(_classificationService);
        Services.AddSingleton(_exportsService);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTime(2026, 11, 4, 12, 0, 0,
            DateTimeKind.Utc)));
        Services.AddSingleton(Substitute.For<ITransfersService>());
        Services.AddSingleton(billsService);
        Services.AddSingleton(debtsService);
        Services.AddSingleton(goalsService);

        // A desktop window, where the grid with its row menus shows rather than the phone list
        IBrowserViewportService viewport = Substitute.For<IBrowserViewportService>();
        viewport.SubscribeAsync(Arg.Any<IBrowserViewportObserver>(), Arg.Any<bool>())
            .Returns(call =>
            {
                _viewportObservers.Add(call.Arg<IBrowserViewportObserver>());
                return Task.CompletedTask;
            });
        Services.AddSingleton(viewport);
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
    }

    [Test]
    public async Task Menu_Pending_CantBeSplitYet()
    {
        // Arrange
        Show(TransactionListTests.Transaction("Supermarket", -100m, pending: true));
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<YourTransactions> cut = await RenderPageAsync();

        // Act
        IReadOnlyList<(string Text, bool Disabled)> items = await RowMenu.ItemsAsync(cut, popovers, "Supermarket");

        // Assert
        await Assert.That(items).Contains(("Split once it's settled", true));
    }

    [Test]
    public async Task Menu_Split_OffersToEditOrRemoveItButNotToTag()
    {
        // Arrange
        Show(TransactionListTests.Transaction("Supermarket", -100m, splits:
        [
            new TransactionSplitPart { Amount = -60m, Tag = "Groceries" },
            new TransactionSplitPart { Amount = -40m }
        ]));
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<YourTransactions> cut = await RenderPageAsync();

        // Act
        IReadOnlyList<(string Text, bool Disabled)> items = await RowMenu.ItemsAsync(cut, popovers, "Supermarket");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(items).Contains(("Edit split", false));
            await Assert.That(items).Contains(("Remove split", false));
            await Assert.That(items.Select(static x => x.Text)).DoesNotContain("Add tags");
        }
    }

    [Test]
    public async Task Split_SavesThePartsAndShowsTheSplit()
    {
        // Arrange
        using CultureScope culture = new();
        Transaction transaction = TransactionListTests.Transaction("Supermarket", -100m);
        Show(transaction);
        _transactionsService.SplitAsync(transaction.TransactionId, Arg.Any<SplitTransaction>(),
                Arg.Any<CancellationToken>())
            .Returns(TransactionListTests.Transaction("Supermarket", -100m, id: transaction.TransactionId, splits:
            [
                new TransactionSplitPart { Amount = -60m },
                new TransactionSplitPart { Amount = -40m }
            ]));
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        Render<MudSnackbarProvider>();
        IRenderedComponent<YourTransactions> cut = await RenderPageAsync();
        Task splitting = await RowMenu.StartAsync(cut, popovers, "Supermarket", "Split");
        dialogs.WaitForElements(".ft-split__amount input")[0].Input("60");
        dialogs.WaitForState(() => Remaining(dialogs) == "£40.00 left to split");

        // Act
        dialogs.FindAll("button").Single(static x => x.GetAttribute("aria-label") == "Put the rest in part 2").Click();
        dialogs.WaitForState(() => Remaining(dialogs) == "The parts add up");
        dialogs.FindAll("button").Single(static x => x.TextContent.Trim() == "Save").Click();
        await splitting.WaitAsync(TestTimeouts.Wait);

        // Assert
        cut.WaitForAssertion(() => _transactionsService.Received(1).SplitAsync(transaction.TransactionId,
            Arg.Is<SplitTransaction>(x => x.Parts.Select(p => p.Amount).SequenceEqual(new[] { -60m, -40m })),
            Arg.Any<CancellationToken>()));
        cut.WaitForState(() => cut.Markup.Contains("Split 2 ways"));
        await Assert.That(cut.Markup).Contains("Split 2 ways");
    }

    [Test]
    public async Task RemoveSplit_AsksFirstThenPutsItBackTogether()
    {
        // Arrange
        Transaction transaction = TransactionListTests.Transaction("Supermarket", -100m, splits:
        [
            new TransactionSplitPart { Amount = -60m },
            new TransactionSplitPart { Amount = -40m }
        ]);
        Show(transaction);
        _transactionsService.UnsplitAsync(transaction.TransactionId, Arg.Any<CancellationToken>())
            .Returns(TransactionListTests.Transaction("Supermarket", -100m, id: transaction.TransactionId));
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        Render<MudSnackbarProvider>();
        IRenderedComponent<YourTransactions> cut = await RenderPageAsync();
        Task removing = await RowMenu.StartAsync(cut, popovers, "Supermarket", "Remove split");

        // Act
        await RowMenu.ConfirmAsync(dialogs, "Remove split");
        await removing.WaitAsync(TestTimeouts.Wait);

        // Assert
        await _transactionsService.Received(1).UnsplitAsync(transaction.TransactionId, Arg.Any<CancellationToken>());
        cut.WaitForState(() => !cut.Markup.Contains("Split 2 ways"));
    }

    [Test]
    public async Task RemoveSplit_WhenItFails_SaysSoAndKeepsTheSplit()
    {
        // Arrange
        Transaction transaction = TransactionListTests.Transaction("Supermarket", -100m, splits:
        [
            new TransactionSplitPart { Amount = -60m },
            new TransactionSplitPart { Amount = -40m }
        ]);
        Show(transaction);
        _transactionsService.UnsplitAsync(transaction.TransactionId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The server didn't answer"));
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<YourTransactions> cut = await RenderPageAsync();
        Task removing = await RowMenu.StartAsync(cut, popovers, "Supermarket", "Remove split");

        // Act
        await RowMenu.ConfirmAsync(dialogs, "Remove split");
        await removing.WaitAsync(TestTimeouts.Wait);

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Couldn't remove the split, try again"));
        await Assert.That(cut.Markup).Contains("Split 2 ways");
    }

    [Test]
    public async Task AddTags_BeforeAnyAreMade_SaysToMakeSomeFirst()
    {
        // Arrange, no tags yet
        Show(TransactionListTests.Transaction("Supermarket", -100m));
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<YourTransactions> cut = await RenderPageAsync();

        // Act
        await RowMenu.ChooseAsync(cut, popovers, "Supermarket", "Add tags");

        // Assert
        snackbars.WaitForState(() =>
            snackbars.Markup.Contains("You haven't made any tags yet, add some on the Tags page first"));
        await _classificationService.DidNotReceive().AddCustomClassificationsToTransactionAsync(
            Arg.Any<AddCustomClassificationsToTransaction>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExportCsv_SavesWhatTheFiltersShowAsAFile()
    {
        // Arrange
        Show(TransactionListTests.Transaction("Supermarket", -100m));
        IRenderedComponent<YourTransactions> cut = await RenderPageAsync();
        cut.Find("input[placeholder='Search descriptions']").Input("Super");
        cut.WaitForState(() => cut.Find("input[placeholder='Search descriptions']").GetAttribute("value") == "Super");
        await cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Apply").ClickAsync(new());
        cut.WaitForState(() => cut.Markup.Contains("Actions for Supermarket"));

        // Act
        await cut.InvokeAsync(() => cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Export CSV")
            .Click());

        // Assert
        cut.WaitForState(() => JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile"));
        JSRuntimeInvocation saved = JSInterop.Invocations.Single(static x => x.Identifier == "ftDownloadFile");
        using (Assert.Multiple())
        {
            await _exportsService.Received(1).WriteTransactionsCsvAsync(
                Arg.Is<FilterTransactions>(x => x.SearchTerm == "Super"), Arg.Any<Stream>(),
                Arg.Any<CancellationToken>());
            await Assert.That(saved.Arguments[0]).IsEqualTo("fintrack-transactions-2026-11-04.csv");
            await Assert.That(saved.Arguments[1]).IsEqualTo("text/csv");
        }
    }

    [Test]
    public async Task ExportCsv_WhenItFails_SaysSo()
    {
        // Arrange
        Show(TransactionListTests.Transaction("Supermarket", -100m));
        _exportsService.WriteTransactionsCsvAsync(Arg.Any<FilterTransactions>(), Arg.Any<Stream>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The server didn't answer"));
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<YourTransactions> cut = await RenderPageAsync();

        // Act
        await cut.InvokeAsync(() => cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Export CSV")
            .Click());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Couldn't export your transactions, try again"));
        await Assert.That(JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile")).IsFalse();
    }

    [Test]
    public async Task Synced_RefreshesWithTheSameFiltersAndTheirOptions()
    {
        // Arrange
        Show(TransactionListTests.Transaction("Supermarket", -100m));
        IRenderedComponent<YourTransactions> cut = await RenderPageAsync();
        AngleSharp.Dom.IElement search = cut.Find("input[placeholder='Search descriptions']");
        search.Input("Super");
        cut.WaitForState(() => cut.Find("input[placeholder='Search descriptions']").GetAttribute("value") == "Super");
        await cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Apply").ClickAsync(new());
        cut.WaitForState(() => cut.Markup.Contains("Actions for Supermarket"));
        _transactionsService.GetAllTransactionsAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                TransactionListTests.Transaction("Supermarket", -100m),
                TransactionListTests.Transaction("Supermarket petrol", -40m)
            }.ToAsyncEnumerable());
        _transactionsService.ClearReceivedCalls();

        // Act
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Actions for Supermarket petrol"));
        using (Assert.Multiple())
        {
            _transactionsService.Received(1).GetAllTransactionsAsync(
                Arg.Is<FilterTransactions>(x => x.SearchTerm == "Super"), Arg.Any<CancellationToken>());
            _transactionsService.Received(1).GetAccountsForTransactionFiltersAsync(Arg.Any<CancellationToken>());
            await Assert.That(cut.Find("input[placeholder='Search descriptions']").GetAttribute("value"))
                .IsEqualTo("Super");
        }
    }

    private static string Remaining(IRenderedComponent<MudDialogProvider> dialogs) =>
        dialogs.Find(".ft-split__remaining").TextContent.Trim();

    private void Show(Transaction transaction) =>
        _transactionsService.GetAllTransactionsAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(new[] { transaction }.ToAsyncEnumerable());

    private async Task<IRenderedComponent<YourTransactions>> RenderPageAsync()
    {
        IRenderedComponent<YourTransactions> cut =
            Render<YourTransactions>(p => p.AddCascadingValue(_applicationState));
        cut.WaitForState(() => cut.Markup.Contains("1 transaction"));

        // The window is desktop width
        BrowserViewportEventArgs desktop = new(Guid.NewGuid(), new BrowserWindowSize { Width = 1280, Height = 800 },
            Breakpoint.Lg, isImmediate: true);
        foreach (IBrowserViewportObserver observer in _viewportObservers.ToList())
        {
            await cut.InvokeAsync(() => observer.NotifyBrowserViewportChangeAsync(desktop));
        }

        cut.WaitForElement("[aria-label='Actions for Supermarket']");
        return cut;
    }
}
