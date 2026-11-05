using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReportsPage = FinanceTracker.Client.Pages.Reports;

namespace FinanceTracker.Tests.Unit.Client.Reports;

public class ReportsPageTests : ComponentTestBase
{
    private readonly IReportService _reportService = Substitute.For<IReportService>();
    private readonly IExportsService _exportsService = Substitute.For<IExportsService>();
    private readonly ApplicationState _applicationState = new();

    public ReportsPageTests()
    {
        AddBunitPersistentComponentState();

        ITransactionsService transactionsService = Substitute.For<ITransactionsService>();
        transactionsService.GetAccountsForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<TransactionAccountFilter>());
        transactionsService.GetProvidersForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<TransactionProviderFilter>());
        transactionsService.GetTypesForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<TransactionTypeFilter>());
        transactionsService.GetCategoriesForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<TransactionCategoryFilter>());
        transactionsService.GetTagsForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<TransactionTagFilter>());
        IBillsService billsService = Substitute.For<IBillsService>();
        billsService.GetBillsAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<Bill>());
        IDebtsService debtsService = Substitute.For<IDebtsService>();
        debtsService.GetDebtsAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<Debt>());
        ISavingsGoalsService goalsService = Substitute.For<ISavingsGoalsService>();
        goalsService.GetSavingsGoalsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<SavingsGoal>());

        _reportService.GetSpentInTimePeriodReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<SpentInTimePeriodReport>());
        _reportService.GetAccountBreakdownReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(_ => AsyncEnumerable.Empty<SpentInAccountReport>());
        _reportService.GetCategoryBreakdownReportAsync(Arg.Any<FilterTransactions>(), Arg.Any<CancellationToken>())
            .Returns(_ => new[]
            {
                new SpentInCategoryReport { Category = "Groceries", TotalOut = -40m, TotalTransactions = 2 }
            }.ToAsyncEnumerable());

        Services.AddSingleton(_reportService);
        Services.AddSingleton(_exportsService);
        Services.AddSingleton(transactionsService);
        Services.AddSingleton(billsService);
        Services.AddSingleton(debtsService);
        Services.AddSingleton(goalsService);
        Services.AddSingleton<TimeProvider>(
            new FixedTimeProvider(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc)));
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
    }

    [Test]
    public async Task ExportPdf_SavesTheBreakdownShown()
    {
        // Arrange
        IRenderedComponent<ReportsPage> cut = RenderPage("tags");

        // Act
        await cut.InvokeAsync(() => ExportButton(cut).Click());

        // Assert
        cut.WaitForState(() => JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile"));
        JSRuntimeInvocation saved = JSInterop.Invocations.Single(static x => x.Identifier == "ftDownloadFile");
        using (Assert.Multiple())
        {
            await _exportsService.Received(1).WriteReportAsync(
                Arg.Is<ExportReport>(static x => x.Breakdown == ReportBreakdown.ByTag), Arg.Any<Stream>(),
                Arg.Any<CancellationToken>());
            await Assert.That(saved.Arguments[0]).IsEqualTo("fintrack-report-by-tag-2026-11-04.pdf");
            await Assert.That(saved.Arguments[1]).IsEqualTo("application/pdf");
        }
    }

    [Test]
    public async Task ExportPdf_NothingToShow_CantBePressed()
    {
        // Arrange: nothing over time
        IRenderedComponent<ReportsPage> cut = RenderPage(null);
        cut.WaitForState(() => cut.Markup.Contains("No transactions match these filters"));

        // Assert
        await Assert.That(ExportButton(cut).HasAttribute("disabled")).IsTrue();
    }

    [Test]
    public async Task ExportPdf_WhenItFails_SaysSo()
    {
        // Arrange
        _exportsService.WriteReportAsync(Arg.Any<ExportReport>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The server didn't answer"));
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<ReportsPage> cut = RenderPage("tags");

        // Act
        await cut.InvokeAsync(() => ExportButton(cut).Click());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Couldn't export your report, try again"));
        await Assert.That(JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile")).IsFalse();
    }

    private IRenderedComponent<ReportsPage> RenderPage(string? breakdown)
    {
        IRenderedComponent<ReportsPage> cut = Render<ReportsPage>(p => p
            .Add(x => x.BreakdownSlug, breakdown)
            .AddCascadingValue(_applicationState));
        cut.WaitForState(() => !cut.Markup.Contains("mud-progress"));
        return cut;
    }

    private static AngleSharp.Dom.IElement ExportButton(IRenderedComponent<ReportsPage> cut) =>
        cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Export PDF");
}
