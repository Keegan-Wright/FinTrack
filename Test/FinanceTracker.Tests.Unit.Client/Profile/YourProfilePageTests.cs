using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static FinanceTracker.Tests.Unit.Client.Profile.TestProfiles;

namespace FinanceTracker.Tests.Unit.Client.Profile;

public class YourProfilePageTests : ComponentTestBase
{
    private readonly IProfileService _profileService = Substitute.For<IProfileService>();

    public YourProfilePageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_profileService);
        Services.AddSingleton(Substitute.For<IPasskeyService>());
        Services.AddScoped<IValidator<ChangeEmail>, ChangeEmailValidator>();
        Services.AddScoped<IValidator<ChangePassword>, ChangePasswordValidator>();
    }

    [Test]
    public async Task Render_LocalAccount_HasEmailPasswordAndSecurityTabsOpenOnEmail()
    {
        // Arrange
        _profileService.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(Create());

        // Act
        IRenderedComponent<YourProfile> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.FindAll(".mud-tab").Count == 3);
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Signed in as sam");
            await Assert.That(cut.FindAll(".mud-tab").Select(static x => x.TextContent.Trim()))
                .IsEquivalentTo(["Email", "Password", "Security"],
                    TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(cut.FindAll(".mud-button-root").Select(static x => x.TextContent.Trim()))
                .Contains("Change email");
        }
    }

    [Test]
    public async Task Render_NoPassword_SaysTheProviderLooksAfterThem()
    {
        // Arrange, made by signing in through the identity provider
        _profileService.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(Create(hasPassword: false));

        // Act
        IRenderedComponent<YourProfile> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("identity provider", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("You sign in through your identity provider");
            await Assert.That(cut.FindAll("button")).IsEmpty();
            await Assert.That(cut.FindAll("input")).IsEmpty();
        }
    }

    private IRenderedComponent<YourProfile> RenderPage() =>
        Render<YourProfile>(p => p.AddCascadingValue(new ApplicationState()));
}
