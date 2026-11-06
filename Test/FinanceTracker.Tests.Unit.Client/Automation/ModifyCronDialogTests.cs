using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Dialogs;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Automation;

public class ModifyCronDialogTests : ComponentTestBase
{
    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private IDialogReference _dialog = null!;

    public ModifyCronDialogTests() =>
        Services.AddScoped<IValidator<UpdateCronJob>, CronJobUpdateRequestValidator>();

    [Test]
    public async Task Ok_Unchanged_ReturnsTheJobAsItWas()
    {
        // Arrange
        await OpenAsync();

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        UpdateCronJob saved = (UpdateCronJob)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(saved.Function).IsEqualTo("SyncAll");
            await Assert.That(saved.Expression).IsEqualTo("0 0 */4 * * ?");
            await Assert.That(saved.RetryIntervals).IsEquivalentTo([10, 20]);
            await Assert.That(saved.IsEnabled).IsTrue();
        }
    }

    [Test]
    public async Task RetryAfter_NewSeconds_ReturnsThem()
    {
        // Arrange
        await OpenAsync();
        Field("Retry after (seconds)").Input("5, 15, 60");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        await Assert.That(((UpdateCronJob)result!.Data!).RetryIntervals).IsEquivalentTo([5, 15, 60]);
    }

    [Test]
    [Arguments("10, abc, 30")]
    [Arguments("10, -5")]
    [Arguments("1.5")]
    public async Task RetryAfter_NotAllWholeSeconds_SaysSoAndKeepsTheDialogOpen(string typed)
    {
        // Arrange
        await OpenAsync();
        Field("Retry after (seconds)").Input(typed);

        // Act
        Button("OK").Click();

        // Assert, what was typed stays to fix
        _dialogs.WaitForState(() => _dialogs.Markup.Contains("Whole seconds, separated by commas"));
        using (Assert.Multiple())
        {
            await Assert.That(_dialog.Result.IsCompleted).IsFalse();
            await Assert.That(Field("Retry after (seconds)").GetAttribute("value")).IsEqualTo(typed);
        }
    }

    [Test]
    public async Task Schedule_NotAQuartzCron_SaysSoAndKeepsTheDialogOpen()
    {
        // Arrange, five parts as a Unix cron has
        await OpenAsync();
        Field("Cron schedule (UTC)").Input("0 */4 * * *");

        // Act
        Button("OK").Click();

        // Assert
        _dialogs.WaitForState(() => _dialogs.Markup.Contains("Expression must be a Quartz cron"));
        await Assert.That(_dialog.Result.IsCompleted).IsFalse();
    }

    private async Task OpenAsync()
    {
        Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<ModifyCronDialog> parameters = new()
        {
            { x => x.Function, "SyncAll" },
            { x => x.Description, "Sync every bank" },
            { x => x.Expression, "0 0 */4 * * ?" },
            { x => x.Retries, 2 },
            { x => x.RetryIntervals, new[] { 10, 20 } },
            { x => x.IsEnabled, true }
        };

        _dialog = await _dialogs.InvokeAsync(() => dialogService.ShowAsync<ModifyCronDialog>(null, parameters));
    }

    private IElement Field(string label) =>
        _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label)
            .QuerySelector("input")!;

    private IElement Button(string text) =>
        _dialogs.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
