using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Dialogs;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Household;

public class RecogniseHouseholdMemberDialogTests : ComponentTestBase
{
    private static readonly HouseholdMember Partner = new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Jane",
        LastName = "Smith",
        PayeeNames = ["J Smith"],
        LinkedAccounts = [new LinkedAccount { SortCode = "112233", Number = "12345678" }]
    };

    private IRenderedComponent<MudDialogProvider> _provider = null!;
    private IDialogReference _dialog = null!;

    public RecogniseHouseholdMemberDialogTests() =>
        Services.AddScoped<IValidator<LinkedAccount>, LinkedAccountValidator>();

    [Test]
    public async Task Render_ShowsTheirNamesAndAccounts()
    {
        // Act
        await OpenAsync(Partner);

        // Assert
        IReadOnlyList<string> chips = _provider.FindAll(".mud-chip").Select(static x => x.TextContent.Trim()).ToList();
        await Assert.That(chips).IsEquivalentTo(["J Smith", "11-22-33 12345678"]);
    }

    [Test]
    public async Task Save_AfterAddingAName_ReturnsItWithTheExistingOnes()
    {
        // Arrange
        await OpenAsync(Partner);
        Field("Name").Input("Jane Smith");
        Button("Add name").Click();

        // Act
        UpdateHouseholdMemberRecognition? saved = await SaveAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(saved!.PayeeNames).IsEquivalentTo(["J Smith", "Jane Smith"]);
            await Assert.That(saved.LinkedAccounts.Single().Number).IsEqualTo("12345678");
        }
    }

    [Test]
    public async Task Save_AfterRemovingAName_LeavesItOut()
    {
        // Arrange
        await OpenAsync(Partner);
        _provider.Find(".mud-chip .mud-chip-close-button").Click();

        // Act
        UpdateHouseholdMemberRecognition? saved = await SaveAsync();

        // Assert
        await Assert.That(saved!.PayeeNames).IsEmpty();
    }

    [Test]
    public async Task Save_WithAnAccountTypedButNotAdded_IncludesItAsDigits()
    {
        // Arrange
        await OpenAsync(Partner);
        Field("Sort code").Input("22-00-40");
        Field("Account number").Input("2058 9674");

        // Act
        UpdateHouseholdMemberRecognition? saved = await SaveAsync();

        // Assert
        await Assert.That(saved!.LinkedAccounts.Select(static x => $"{x.SortCode} {x.Number}"))
            .IsEquivalentTo(["112233 12345678", "220040 20589674"]);
    }

    [Test]
    public async Task Save_WithAnInvalidAccountTyped_StaysOpenAndSaysWhy()
    {
        // Arrange
        await OpenAsync(Partner);
        Field("Sort code").Input("1122");
        Field("Account number").Input("12345678");

        // Act
        Button("OK").Click();

        // Assert
        _provider.WaitForState(() => _provider.Markup.Contains("Sort code must be 6 digits"));
        await Assert.That(_dialog.Result.IsCompleted).IsFalse();
    }

    [Test]
    [Arguments("112233", "12345678", "11-22-33 12345678")]
    [Arguments("11-22-33", "1234 5678", "11-22-33 12345678")]
    [Arguments("1122", "12345678", "1122 12345678")]
    public async Task Display_WritesTheSortCodeWithDashes(string sortCode, string number, string expected)
    {
        // Act
        string display = RecogniseHouseholdMemberDialog.Display(new LinkedAccount { SortCode = sortCode, Number = number });

        // Assert
        await Assert.That(display).IsEqualTo(expected);
    }

    private async Task OpenAsync(HouseholdMember member)
    {
        _provider = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<RecogniseHouseholdMemberDialog> { { x => x.Member, member } };

        _dialog = await _provider.InvokeAsync(() =>
            dialogService.ShowAsync<RecogniseHouseholdMemberDialog>(null, parameters));
    }

    private async Task<UpdateHouseholdMemberRecognition?> SaveAsync()
    {
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        return result?.Data as UpdateHouseholdMemberRecognition;
    }

    private IElement Field(string label) =>
        _provider.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label)
            .QuerySelector("input")!;

    private IElement Button(string text) =>
        _provider.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
