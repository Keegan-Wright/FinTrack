using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Client.Household;

public class HouseholdMembersPageTests : ComponentTestBase
{
    private readonly IHouseholdMemberService _householdMemberService = Substitute.For<IHouseholdMemberService>();

    private readonly HouseholdMember _partner = new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Jane",
        LastName = "Smith",
        YearlyIncome = 24_000m
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
    public async Task LinkingToSomeoneTheyShareWith_SavesIt_AndOffersToUnlink()
    {
        // Arrange
        UserLink sam = new()
        {
            Id = Guid.NewGuid(), Name = "Sam Taylor", Username = "sam", State = LinkState.Linked,
            Since = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc)
        };
        ISharingService sharing = Substitute.For<ISharingService>();
        sharing.GetPeopleAsync(Arg.Any<CancellationToken>()).Returns(_ => new[] { sam }.ToAsyncEnumerable());
        Services.AddSingleton(sharing);
        _householdMemberService.LinkHouseholdMemberAsync(_partner.Id, Arg.Any<LinkHouseholdMember>(),
                Arg.Any<CancellationToken>())
            .Returns(new HouseholdMember
            {
                Id = _partner.Id, FirstName = "Sam", LastName = "Taylor", YearlyIncome = 24_000m,
                LinkedUserId = Guid.NewGuid(), LinkedUsername = "sam"
            });
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<HouseholdMembers> cut = Render<HouseholdMembers>(p => p.AddCascadingValue(_applicationState));

        // Act
        await RowMenu.ChooseAsync(cut, popovers, "Jane Smith", "This is Sam Taylor");
        cut.WaitForState(() => cut.Markup.Contains("This is @sam"));
        IReadOnlyList<(string Text, bool Disabled)> after = await RowMenu.ItemsAsync(cut, popovers, "Sam Taylor");

        // Assert
        using (Assert.Multiple())
        {
            await _householdMemberService.Received(1).LinkHouseholdMemberAsync(_partner.Id,
                Arg.Is<LinkHouseholdMember>(x => x.LinkId == sam.Id), Arg.Any<CancellationToken>());
            await Assert.That(after.Select(static x => x.Text)).Contains("Not @sam any more");
        }
    }

    [Test]
    public async Task Edit_SavesTheNewIncomeAndShowsIt()
    {
        // Arrange
        using CultureScope culture = new();
        _householdMemberService.UpdateHouseholdMemberAsync(_partner.Id, Arg.Any<UpdateHouseholdMember>(),
                Arg.Any<CancellationToken>())
            .Returns(new HouseholdMember { Id = _partner.Id, FirstName = "Jane", LastName = "Smith", YearlyIncome = 2500m });
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<HouseholdMembers> cut = Render<HouseholdMembers>(p => p.AddCascadingValue(_applicationState));

        // Act
        Task choosing = await RowMenu.StartAsync(cut, popovers, "Jane Smith", "Edit");
        Field(dialogs, "Yearly income").Input("2500");
        await RowMenu.ConfirmAsync(dialogs, "OK");
        await choosing;
        cut.WaitForState(() => cut.Markup.Contains("£2,500.00"));

        // Assert
        using (Assert.Multiple())
        {
            await _householdMemberService.Received(1).UpdateHouseholdMemberAsync(_partner.Id,
                Arg.Is<UpdateHouseholdMember>(x => x.FirstName == "Jane" && x.LastName == "Smith" &&
                                                   x.YearlyIncome == 2500m),
                Arg.Any<CancellationToken>());
            await Assert.That(cut.Markup).Contains("£2,500.00");
        }
    }

    [Test]
    public async Task Render_TheirYearlyIncome_ShowsWhatItIsAMonthAndTheHouseholdsTogether()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<HouseholdMembers> cut = Render<HouseholdMembers>(p => p.AddCascadingValue(_applicationState));

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Jane Smith", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("£24,000.00");
            await Assert.That(cut.Markup).Contains("a year · £2,000.00 a month");
            await Assert.That(cut.Markup).Contains("1 person · £24,000.00 a year combined, £2,000.00 a month");
        }
    }

    [Test]
    public async Task Edit_ToSomeoneElsesName_SaysSo()
    {
        // Arrange
        _householdMemberService.UpdateHouseholdMemberAsync(_partner.Id, Arg.Any<UpdateHouseholdMember>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new NameTakenException(NameTakenException.Person, "Sam Smith"));
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<HouseholdMembers> cut =
            Render<HouseholdMembers>(p => p.AddCascadingValue(_applicationState));

        // Act
        Task choosing = await RowMenu.StartAsync(cut, popovers, "Jane Smith", "Edit");
        Field(dialogs, "First name").Input("Sam");
        await RowMenu.ConfirmAsync(dialogs, "OK");
        await choosing;

        // Assert
        await Assert.That(snackbars.Markup).Contains("You already have a person called Sam Smith");
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
