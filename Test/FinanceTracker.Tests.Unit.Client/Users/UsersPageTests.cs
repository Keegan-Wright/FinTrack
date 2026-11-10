using Bunit;
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
        Id = Guid.NewGuid(), Username = "sam", Email = "sam@example.test", EmailConfirmed = true,
        HasPassword = true, UsesIdentityProvider = false, IsAdmin = true
    };

    private static readonly UserSummary Provider = new()
    {
        Id = Guid.NewGuid(), Username = "alex", Email = "alex@example.test", EmailConfirmed = false,
        HasPassword = false, UsesIdentityProvider = true, IsAdmin = false
    };

    private readonly IUsersService _service = Substitute.For<IUsersService>();

    public UsersPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_service);
        Services.AddSingleton(TimeProvider.System);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _service.GetUsersAsync(Arg.Any<CancellationToken>()).Returns(new[] { Provider, Local }.ToAsyncEnumerable());
    }

    [Test]
    public async Task Render_SaysHowEachSignsInWithAResetLinkOnlyForAPassword()
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
            await Assert.That(local.QuerySelectorAll("button")).Count().IsEqualTo(1);
            await Assert.That(provider.TextContent).Contains("alex@example.test · Not confirmed");
            await Assert.That(provider.TextContent).Contains("Signs in through the identity provider");
            await Assert.That(provider.QuerySelectorAll("button")).IsEmpty();
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
        _service.CreateResetLinkAsync(Local.Id, Arg.Any<CancellationToken>()).Returns(link);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<UsersPage> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-users__row").Count == 2);

        // Act
        await Row(cut, "sam").QuerySelector("button")!.ClickAsync(new());

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

    private IRenderedComponent<UsersPage> RenderPage() =>
        Render<UsersPage>(p => p.AddCascadingValue(new ApplicationState()));

    private static AngleSharp.Dom.IElement Row(IRenderedComponent<UsersPage> cut, string username) =>
        cut.FindAll(".ft-users__row").Single(x => x.QuerySelector(".ft-users__username")!.TextContent.Trim() == username);
}
