using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Household;

public class HouseholdMembersPageTests : ComponentTestBase
{
    private readonly IHouseholdMemberService _householdMemberService = Substitute.For<IHouseholdMemberService>();

    private readonly HouseholdMember _partner = new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Jane",
        LastName = "Smith",
        Income = 2000m
    };

    private readonly ApplicationState _applicationState = new();

    public HouseholdMembersPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddScoped<IValidator<AddHouseholdMember>, AddHouseholdMemberRequestValidator>();
        _householdMemberService.GetHouseholdMembersAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new[] { _partner }.ToAsyncEnumerable());
        Services.AddSingleton(_householdMemberService);
    }

    [Test]
    public async Task Menu_OffersEditRecogniseAndDelete()
    {
        // Arrange
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<HouseholdMembers> cut = Render<HouseholdMembers>(p => p.AddCascadingValue(_applicationState));

        // Act
        IReadOnlyList<(string Text, bool Disabled)> items = await RowMenu.ItemsAsync(cut, popovers, "Jane Smith");

        // Assert
        await Assert.That(items.Select(static x => x.Text)).IsEquivalentTo(
            ["Edit", "Recognise their payments", "Delete"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Edit_SavesTheNewIncomeAndShowsIt()
    {
        // Arrange
        using CultureScope culture = new();
        _householdMemberService.UpdateHouseholdMemberAsync(_partner.Id, Arg.Any<UpdateHouseholdMember>(),
                Arg.Any<CancellationToken>())
            .Returns(new HouseholdMember { Id = _partner.Id, FirstName = "Jane", LastName = "Smith", Income = 2500m });
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<HouseholdMembers> cut = Render<HouseholdMembers>(p => p.AddCascadingValue(_applicationState));

        // Act
        Task choosing = await RowMenu.StartAsync(cut, popovers, "Jane Smith", "Edit");
        Field(dialogs, "Income").Input("2500");
        await RowMenu.ConfirmAsync(dialogs, "OK");
        await choosing;
        cut.WaitForState(() => cut.Markup.Contains("£2,500.00"));

        // Assert
        using (Assert.Multiple())
        {
            await _householdMemberService.Received(1).UpdateHouseholdMemberAsync(_partner.Id,
                Arg.Is<UpdateHouseholdMember>(x => x.FirstName == "Jane" && x.LastName == "Smith" &&
                                                   x.Income == 2500m),
                Arg.Any<CancellationToken>());
            await Assert.That(cut.Markup).Contains("£2,500.00");
        }
    }

    [Test]
    public async Task Delete_Confirmed_DeletesThemAndTakesThemOffTheList()
    {
        // Arrange
        _householdMemberService.DeleteHouseholdMemberAsync(_partner.Id, Arg.Any<CancellationToken>()).Returns(true);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<HouseholdMembers> cut = Render<HouseholdMembers>(p => p.AddCascadingValue(_applicationState));

        // Act
        Task choosing = await RowMenu.StartAsync(cut, popovers, "Jane Smith", "Delete");
        await RowMenu.ConfirmAsync(dialogs, "Delete");
        await choosing;

        // Assert
        using (Assert.Multiple())
        {
            await _householdMemberService.Received(1)
                .DeleteHouseholdMemberAsync(_partner.Id, Arg.Any<CancellationToken>());
            await Assert.That(cut.FindAll("[aria-label='Actions for Jane Smith']")).IsEmpty();
        }
    }

    private static IElement Field(IRenderedComponent<MudDialogProvider> dialogs, string label) =>
        dialogs.WaitForElements(".mud-dialog .mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label)
            .QuerySelector("input")!;
}
