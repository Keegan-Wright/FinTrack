using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Client.Classifications;

public class CustomClassificationsPageTests : ComponentTestBase
{
    private readonly IClassificationService _classificationService = Substitute.For<IClassificationService>();
    private readonly Classification _holiday = new() { ClassificationId = Guid.NewGuid(), Tag = "Holiday" };
    private readonly Classification _kids = new() { ClassificationId = Guid.NewGuid(), Tag = "Kids" };

    private readonly ApplicationState _applicationState = new();

    public CustomClassificationsPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddScoped<IValidator<AddClassifications>, AddClassificationsRequestValidator>();
        _classificationService.GetAllCustomClassificationsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new[] { _holiday, _kids }.ToAsyncEnumerable());
        Services.AddSingleton(_classificationService);
    }

    [Test]
    public async Task Rename_SavesTheNewNameAndShowsIt()
    {
        // Arrange
        _classificationService.RenameCustomClassificationAsync(_holiday.ClassificationId,
                Arg.Any<RenameClassification>(), Arg.Any<CancellationToken>())
            .Returns(new Classification { ClassificationId = _holiday.ClassificationId, Tag = "Travel" });
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<CustomClassifications> cut = Render<CustomClassifications>(p => p.AddCascadingValue(_applicationState));

        // Act
        Task choosing = await RowMenu.StartAsync(cut, popovers, "Holiday", "Rename");
        IElement field = dialogs.WaitForElement(".mud-dialog input");
        string shown = field.GetAttribute("value") ?? string.Empty;
        field.Input("Travel");
        await RowMenu.ConfirmAsync(dialogs, "OK");
        await choosing;
        cut.WaitForState(() => cut.Markup.Contains("Travel"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(shown).IsEqualTo("Holiday");
            await _classificationService.Received(1).RenameCustomClassificationAsync(_holiday.ClassificationId,
                Arg.Is<RenameClassification>(x => x.Tag == "Travel"), Arg.Any<CancellationToken>());
            await Assert.That(cut.Markup).Contains("Travel");
        }
    }

    [Test]
    public async Task Rename_ToATagTheyAlreadyHave_SaysSoAndKeepsTheName()
    {
        // Arrange
        _classificationService.RenameCustomClassificationAsync(_holiday.ClassificationId,
                Arg.Any<RenameClassification>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateTagException("Kids"));
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<CustomClassifications> cut =
            Render<CustomClassifications>(p => p.AddCascadingValue(_applicationState));

        // Act
        Task choosing = await RowMenu.StartAsync(cut, popovers, "Holiday", "Rename");
        dialogs.WaitForElement(".mud-dialog input").Input("Kids");
        await RowMenu.ConfirmAsync(dialogs, "OK");
        await choosing;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(snackbars.Markup).Contains("You already have a tag called Kids");
            await Assert.That(cut.FindAll("[aria-label='Actions for Holiday']")).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task Delete_Confirmed_DeletesTheTagAndTakesItOffTheList()
    {
        // Arrange
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<CustomClassifications> cut = Render<CustomClassifications>(p => p.AddCascadingValue(_applicationState));

        // Act
        Task choosing = await RowMenu.StartAsync(cut, popovers, "Kids", "Delete");
        await RowMenu.ConfirmAsync(dialogs, "Delete");
        await choosing;

        // Assert
        using (Assert.Multiple())
        {
            await _classificationService.Received(1)
                .RemoveCustomClassificationAsync(_kids.ClassificationId, Arg.Any<CancellationToken>());
            await Assert.That(cut.FindAll("[aria-label='Actions for Kids']")).IsEmpty();
        }
    }

    [Test]
    public async Task Delete_Cancelled_KeepsTheTag()
    {
        // Arrange
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<CustomClassifications> cut = Render<CustomClassifications>(p => p.AddCascadingValue(_applicationState));

        // Act
        Task choosing = await RowMenu.StartAsync(cut, popovers, "Kids", "Delete");
        await RowMenu.ConfirmAsync(dialogs, "Cancel");
        await choosing;

        // Assert
        await _classificationService.DidNotReceive()
            .RemoveCustomClassificationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
