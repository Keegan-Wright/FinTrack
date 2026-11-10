using Bunit;
using FinanceTracker.Client.Components.Profile;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Profile;

public class ChangePasswordSectionTests : ComponentTestBase
{
    private readonly IProfileService _service = Substitute.For<IProfileService>();

    public ChangePasswordSectionTests()
    {
        Services.AddSingleton(_service);
        Services.AddScoped<IValidator<ChangePassword>, ChangePasswordValidator>();
    }

    [Test]
    public async Task Change_Filled_ChangesItAndClearsTheFields()
    {
        // Arrange
        _service.ChangePasswordAsync(Arg.Any<ChangePassword>(), Arg.Any<CancellationToken>())
            .Returns(new ProfileChange());
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<ChangePasswordSection> cut = Render<ChangePasswordSection>();

        // Act
        await FillAsync(cut, "old-password", "new-password", "new-password");
        await ChangeButton(cut).ClickAsync(new());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Password changed", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await _service.Received(1).ChangePasswordAsync(
                Arg.Is<ChangePassword>(x => x.CurrentPassword == "old-password" && x.NewPassword == "new-password"),
                Arg.Any<CancellationToken>());
            await Assert.That(cut.FindAll("input").Select(static x => x.GetAttribute("value") ?? string.Empty))
                .All().Satisfy(static x => x.IsEmpty());
        }
    }

    [Test]
    public async Task Change_Refused_SaysWhy()
    {
        // Arrange
        _service.ChangePasswordAsync(Arg.Any<ChangePassword>(), Arg.Any<CancellationToken>())
            .Returns(new ProfileChange { Errors = ["Your current password isn't right"] });
        IRenderedComponent<ChangePasswordSection> cut = Render<ChangePasswordSection>();

        // Act
        await FillAsync(cut, "wrong", "new-password", "new-password");
        await ChangeButton(cut).ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.FindAll("[role='alert']").Count == 1);
        await Assert.That(cut.Find("[role='alert']").TextContent).Contains("Your current password isn't right");
    }

    [Test]
    public async Task Change_NewPasswordsDiffer_SaysSoWithoutChanging()
    {
        // Arrange
        IRenderedComponent<ChangePasswordSection> cut = Render<ChangePasswordSection>();

        // Act
        await FillAsync(cut, "old-password", "new-password", "new-pasword");
        await ChangeButton(cut).ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("The passwords don't match", StringComparison.Ordinal));
        await _service.DidNotReceive().ChangePasswordAsync(Arg.Any<ChangePassword>(), Arg.Any<CancellationToken>());
    }

    private static async Task FillAsync(IRenderedComponent<ChangePasswordSection> cut, string current,
        string newPassword, string confirm)
    {
        await Field(cut, "Current password").ChangeAsync(new() { Value = current });
        await Field(cut, "New password").ChangeAsync(new() { Value = newPassword });
        await Field(cut, "Confirm new password").ChangeAsync(new() { Value = confirm });
    }

    /// <summary>
    /// The field labelled <paramref name="label"/>, found by its label as a screen reader reads it.
    /// </summary>
    private static AngleSharp.Dom.IElement Field(IRenderedComponent<ChangePasswordSection> cut, string label)
    {
        string id = cut.FindAll("label").Single(x => x.TextContent.Trim() == label).GetAttribute("for")!;
        return cut.Find($"#{id}");
    }

    private static AngleSharp.Dom.IElement ChangeButton(IRenderedComponent<ChangePasswordSection> cut) =>
        cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Change password");
}
