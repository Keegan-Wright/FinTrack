using Bunit;
using Bunit.TestDoubles;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Users;

public class UsersPageTests : ComponentTestBase
{
    private static readonly UserSummary Local = new()
    {
        Id = Guid.NewGuid(), Username = "sam", Name = "Sam Taylor", Email = "sam@example.test", EmailConfirmed = true,
        HasPassword = true, TwoFactorEnabled = false, UsesIdentityProvider = false, IsAdmin = true, IsLocked = false, IsYou = true
    };

    private static readonly UserSummary Provider = new()
    {
        Id = Guid.NewGuid(), Username = "alex", Name = "Alex Smith",
        Email = "alex@example.test", EmailConfirmed = false,
        HasPassword = false, TwoFactorEnabled = false, UsesIdentityProvider = true, IsAdmin = false, IsLocked = false, IsYou = false
    };

    private static readonly UserSummary LockedProvider = new()
    {
        Id = Provider.Id, Username = "alex", Name = "Alex Smith", Email = "alex@example.test", EmailConfirmed = false,
        HasPassword = false, TwoFactorEnabled = false, UsesIdentityProvider = true, IsAdmin = false, IsLocked = true, IsYou = false
    };

    private static readonly UserSummary Jo = new()
    {
        Id = Guid.NewGuid(), Username = "jo", Name = "Jo Brown", Email = "jo@example.test", EmailConfirmed = true,
        HasPassword = true, TwoFactorEnabled = false, UsesIdentityProvider = false, IsAdmin = false, IsLocked = false, IsYou = false
    };

    private static readonly UserSummary JoAsAdmin = new()
    {
        Id = Jo.Id, Username = "jo", Name = "Jo Brown", Email = "jo@example.test", EmailConfirmed = true,
        HasPassword = true, TwoFactorEnabled = false, UsesIdentityProvider = false, IsAdmin = true, IsLocked = false, IsYou = false
    };

    private static readonly UserSummary JoWithTwoFactor = new()
    {
        Id = Jo.Id, Username = "jo", Name = "Jo Brown", Email = "jo@example.test", EmailConfirmed = true,
        HasPassword = true, TwoFactorEnabled = true, UsesIdentityProvider = false, IsAdmin = false, IsLocked = false,
        IsYou = false
    };

    private static readonly UserSummary YouWithTwoFactor = new()
    {
        Id = Local.Id, Username = "sam", Name = "Sam Taylor", Email = "sam@example.test", EmailConfirmed = true,
        HasPassword = true, TwoFactorEnabled = true, UsesIdentityProvider = false, IsAdmin = true, IsLocked = false,
        IsYou = true
    };

    private static readonly UserSummary ProviderAdmin = new()
    {
        Id = Guid.NewGuid(), Username = "kit", Name = "Kit Green", Email = "kit@example.test", EmailConfirmed = true,
        HasPassword = false, TwoFactorEnabled = false, UsesIdentityProvider = true, IsAdmin = true, IsLocked = false, IsYou = false
    };

    private readonly IUsersService _usersService = Substitute.For<IUsersService>();

    public UsersPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_usersService);
        Services.AddSingleton(TimeProvider.System);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _usersService.GetUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { Provider, Local }.ToAsyncEnumerable());
    }

    [Test]
    public async Task Render_SaysHowEachSignsInWithAResetLinkOnlyForAPasswordAndNoLockForAnAdmin()
    {
        // Act
        IRenderedComponent<UsersPage> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);
        AngleSharp.Dom.IElement local = Row(cut, "sam");
        AngleSharp.Dom.IElement provider = Row(cut, "alex");
        using (Assert.Multiple())
        {
            await Assert.That(local.TextContent).Contains("sam@example.test · Confirmed");
            await Assert.That(local.TextContent).Contains("Signs in with a password");
            await Assert.That(local.TextContent).Contains("Admin");
            await Assert.That(local.TextContent).Contains("You");
            await Assert.That(ButtonTexts(local)).IsEquivalentTo(["Reset link", "Remove admin access"]);
            await Assert.That(provider.TextContent).Contains("alex@example.test · Not confirmed");
            await Assert.That(provider.TextContent).Contains("Signs in through the identity provider");
            await Assert.That(ButtonTexts(provider)).IsEquivalentTo(["Lock"]);
            await Assert.That(provider.TextContent).DoesNotContain("Locked");
        }
    }

    [Test]
    public async Task ResetLink_Made_ShowsItToCopy()
    {
        // Arrange
        ResetLink link = new()
        {
            Url = "https://fintrack.example.test/Account/ResetPassword?userId=1&code=abc",
            ExpiresAt = new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc)
        };
        _usersService.CreateResetLinkAsync(Local.Id, Arg.Any<CancellationToken>()).Returns(link);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<UsersPage> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);

        // Act
        await RowButton(cut, "sam", "Reset link").ClickAsync(new());

        // Assert
        dialogs.WaitForState(() => dialogs.FindAll("textarea").Count == 1);
        using (Assert.Multiple())
        {
            await Assert.That(dialogs.Find("textarea").GetAttribute("value") ?? dialogs.Find("textarea").TextContent)
                .Contains(link.Url);
            await Assert.That(dialogs.Markup).Contains("Send this to sam yourself.");
            await Assert.That(dialogs.Markup).Contains("It works once, until");
        }
    }

    [Test]
    public async Task Render_TwoFactorOn_ShowsItWithATurnOffForOthersButNotYourself()
    {
        // Arrange
        _usersService.GetUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { JoWithTwoFactor, YouWithTwoFactor }.ToAsyncEnumerable());

        // Act
        IRenderedComponent<UsersPage> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);
        using (Assert.Multiple())
        {
            await Assert.That(Row(cut, "jo").TextContent).Contains("Two-factor");
            await Assert.That(ButtonTexts(Row(cut, "jo"))).Contains("Turn off two-factor");
            await Assert.That(Row(cut, "sam").TextContent).Contains("Two-factor");
            await Assert.That(ButtonTexts(Row(cut, "sam"))).DoesNotContain("Turn off two-factor");
        }
    }

    [Test]
    public async Task TurnOffTwoFactor_Confirmed_TurnsItOffAndShowsItOff()
    {
        // Arrange
        _usersService.GetUsersAsync(Arg.Any<CancellationToken>()).Returns(
            new[] { JoWithTwoFactor, Local }.ToAsyncEnumerable(), new[] { Jo, Local }.ToAsyncEnumerable());
        _usersService.TurnOffTwoFactorAsync(Jo.Id, Arg.Any<CancellationToken>()).Returns(true);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<UsersPage> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);

        // Act
        Task turningOff = RowButton(cut, "jo", "Turn off two-factor").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "Turn off");
        await turningOff.WaitAsync(TestTimeouts.Wait);

        // Assert
        cut.WaitForState(() => !Row(cut, "jo").TextContent.Contains("Two-factor"));
        await _usersService.Received(1).TurnOffTwoFactorAsync(Jo.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TurnOffTwoFactor_Cancelled_LeavesItOn()
    {
        // Arrange
        _usersService.GetUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { JoWithTwoFactor, Local }.ToAsyncEnumerable());
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<UsersPage> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);

        // Act
        Task turningOff = RowButton(cut, "jo", "Turn off two-factor").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "Cancel");
        await turningOff.WaitAsync(TestTimeouts.Wait);

        // Assert
        await _usersService.DidNotReceive().TurnOffTwoFactorAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Lock_Confirmed_LocksThemAndShowsThemLocked()
    {
        // Arrange
        _usersService.GetUsersAsync(Arg.Any<CancellationToken>()).Returns(
            new[] { Provider, Local }.ToAsyncEnumerable(), new[] { LockedProvider, Local }.ToAsyncEnumerable());
        _usersService.LockAsync(Provider.Id, Arg.Any<CancellationToken>()).Returns(true);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<UsersPage> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);

        // Act
        Task locking = RowButton(cut, "alex", "Lock").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "Lock");
        await locking.WaitAsync(TestTimeouts.Wait);

        // Assert
        cut.WaitForState(() => Row(cut, "alex").TextContent.Contains("Locked"));
        using (Assert.Multiple())
        {
            await _usersService.Received(1).LockAsync(Provider.Id, Arg.Any<CancellationToken>());
            await Assert.That(ButtonTexts(Row(cut, "alex"))).IsEquivalentTo(["Unlock"]);
        }
    }

    [Test]
    public async Task Lock_Cancelled_LeavesThemBe()
    {
        // Arrange
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<UsersPage> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);

        // Act
        Task locking = RowButton(cut, "alex", "Lock").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "Cancel");
        await locking.WaitAsync(TestTimeouts.Wait);

        // Assert
        await _usersService.DidNotReceive().LockAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Unlock_Clicked_UnlocksThemAndShowsTheLockAgain()
    {
        // Arrange
        _usersService.GetUsersAsync(Arg.Any<CancellationToken>()).Returns(
            new[] { LockedProvider, Local }.ToAsyncEnumerable(), new[] { Provider, Local }.ToAsyncEnumerable());
        IRenderedComponent<UsersPage> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);

        // Act
        await RowButton(cut, "alex", "Unlock").ClickAsync(new());

        // Assert
        cut.WaitForState(() => !Row(cut, "alex").TextContent.Contains("Locked"));
        using (Assert.Multiple())
        {
            await _usersService.Received(1).UnlockAsync(Provider.Id, Arg.Any<CancellationToken>());
            await Assert.That(ButtonTexts(Row(cut, "alex"))).IsEquivalentTo(["Lock"]);
        }
    }

    [Test]
    public async Task Render_AdminsAndWhoTheGroupDecidesFor_HaveNoLockOrAdminButtons()
    {
        // Arrange
        _usersService.GetUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { Jo, ProviderAdmin, Local }.ToAsyncEnumerable());

        // Act
        IRenderedComponent<UsersPage> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 3);
        using (Assert.Multiple())
        {
            await Assert.That(ButtonTexts(Row(cut, "jo"))).IsEquivalentTo(["Reset link", "Lock", "Add admin access"]);
            await Assert.That(ButtonTexts(Row(cut, "kit"))).IsEmpty();
            await Assert.That(Row(cut, "kit").TextContent).Contains("Admin from SSO group");
        }
    }

    [Test]
    public async Task MakeAdmin_Confirmed_MakesThemAnAdmin()
    {
        // Arrange
        _usersService.GetUsersAsync(Arg.Any<CancellationToken>()).Returns(
            new[] { Jo, Local }.ToAsyncEnumerable(), new[] { JoAsAdmin, Local }.ToAsyncEnumerable());
        _usersService.GrantAdminAsync(Jo.Id, Arg.Any<CancellationToken>()).Returns(AdminChange.Changed);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<UsersPage> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);

        // Act
        Task granting = RowButton(cut, "jo", "Add admin access").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "Add admin access");
        await granting.WaitAsync(TestTimeouts.Wait);

        // Assert
        cut.WaitForState(() => ButtonTexts(Row(cut, "jo")).Contains("Remove admin access"));
        await _usersService.Received(1).GrantAdminAsync(Jo.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveAdmin_TheirOwnAsTheLastAdmin_SaysToMakeSomeoneElseFirst()
    {
        // Arrange
        _usersService.RemoveAdminAsync(Local.Id, Arg.Any<CancellationToken>()).Returns(AdminChange.LastAdmin);
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<UsersPage> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);

        // Act
        Task removing = RowButton(cut, "sam", "Remove admin access").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "Remove my access");
        await removing.WaitAsync(TestTimeouts.Wait);

        // Assert
        snackbars.WaitForState(() =>
            snackbars.Markup.Contains("You're the only admin, make someone else an admin first",
                StringComparison.Ordinal));
        await Assert.That(Services.GetRequiredService<BunitNavigationManager>().History).IsEmpty();
    }

    private IRenderedComponent<UsersPage> RenderPage() =>
        Render<UsersPage>(p => p.AddCascadingValue(new ApplicationState()));

    private static AngleSharp.Dom.IElement Row(IRenderedComponent<UsersPage> cut, string username) =>
        cut.FindAll(".ft-users__row")
            .Single(x => x.QuerySelector(".ft-users__username")!.TextContent.Trim() == username);

    private static AngleSharp.Dom.IElement RowButton(IRenderedComponent<UsersPage> cut, string username, string text) =>
        Row(cut, username).QuerySelectorAll("button").Single(x => x.TextContent.Trim() == text);

    private static string[] ButtonTexts(AngleSharp.Dom.IElement row) =>
        row.QuerySelectorAll("button").Select(static x => x.TextContent.Trim()).ToArray();
}
