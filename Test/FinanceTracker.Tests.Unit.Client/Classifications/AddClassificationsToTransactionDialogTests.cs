using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Dialogs;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Classifications;

public class AddClassificationsToTransactionDialogTests : ComponentTestBase
{
    private static readonly Guid TransactionId = Guid.NewGuid();

    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private IDialogReference _dialog = null!;

    public AddClassificationsToTransactionDialogTests() =>
        Services.AddScoped<IValidator<AddCustomClassificationsToTransaction>,
            AddCustomClassificationsToTransactionRequestValidator>();

    [Test]
    public async Task Ok_TwoTicked_ReturnsThemForTheTransaction()
    {
        // Arrange
        List<Classification> tags = Tags("Food", "Fuel", "Gifts");
        await OpenAsync(tags);
        await TickAsync("Food");
        await TickAsync("Gifts");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        AddCustomClassificationsToTransaction added = (AddCustomClassificationsToTransaction)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(added.TransactionId).IsEqualTo(TransactionId);
            await Assert.That(added.Classifications.Select(static x => x.ClassificationId))
                .IsEquivalentTo([tags[0].ClassificationId, tags[2].ClassificationId]);
        }
    }

    [Test]
    public async Task Ok_TickedThenUnticked_LeavesItOut()
    {
        // Arrange
        List<Classification> tags = Tags("Food", "Fuel");
        await OpenAsync(tags);
        await TickAsync("Food");
        await TickAsync("Fuel");
        await TickAsync("Food");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        await Assert.That(((AddCustomClassificationsToTransaction)result!.Data!).Classifications
                .Select(static x => x.ClassificationId))
            .IsEquivalentTo([tags[1].ClassificationId]);
    }

    [Test]
    public async Task Ok_NoneTicked_KeepsTheDialogOpen()
    {
        // Arrange
        await OpenAsync(Tags("Food", "Fuel"));

        // Act
        Button("OK").Click();

        // Assert
        _dialogs.WaitForState(() => _dialogs.Markup.Contains("At least one classification must be selected"));
        await Assert.That(_dialog.Result.IsCompleted).IsFalse();
    }

    [Test]
    public async Task Search_ManyTags_ShowsOnlyThoseMatching()
    {
        // Arrange, more than fit at a glance, so they can be searched
        await OpenAsync(Tags("Food", "Fuel", "Gifts", "Gym", "Holidays", "Pets", "Phone", "Rent", "Takeaway"));

        // Act
        _dialogs.Find("input[placeholder='Search tags']").Input("fu");

        // Assert
        _dialogs.WaitForState(() => Labels().Count == 1);
        await Assert.That(Labels()).IsEquivalentTo(["Fuel"]);
    }

    [Test]
    public async Task Render_FewTags_OffersNoSearch()
    {
        // Act
        await OpenAsync(Tags("Food", "Fuel"));

        // Assert
        await Assert.That(_dialogs.FindAll("input[placeholder='Search tags']")).IsEmpty();
    }

    private static List<Classification> Tags(params string[] names) =>
        names.Select(static x => new Classification { ClassificationId = Guid.NewGuid(), Tag = x }).ToList();

    private async Task OpenAsync(List<Classification> tags)
    {
        Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<AddClassificationsToTransactionDialog> parameters = new()
        {
            { x => x.TransactionId, TransactionId },
            { x => x.Classifications, tags }
        };

        _dialog = await _dialogs.InvokeAsync(() =>
            dialogService.ShowAsync<AddClassificationsToTransactionDialog>(null, parameters));
    }

    /// <summary>
    /// Ticks <paramref name="tag"/>'s box, or unticks it when it's ticked, as a click does.
    /// </summary>
    private async Task TickAsync(string tag)
    {
        IElement box = _dialogs.FindAll(".mud-checkbox")
            .Single(x => x.TextContent.Trim() == tag)
            .QuerySelector("input[type=checkbox]")!;
        bool ticked = box.HasAttribute("checked");
        await box.ChangeAsync(new ChangeEventArgs { Value = !ticked });
    }

    private List<string> Labels() =>
        _dialogs.FindAll(".mud-checkbox").Select(static x => x.TextContent.Trim()).ToList();

    private IElement Button(string text) =>
        _dialogs.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
