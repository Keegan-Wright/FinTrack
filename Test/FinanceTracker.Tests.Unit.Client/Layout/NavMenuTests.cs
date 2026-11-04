using Bunit;
using Bunit.TestDoubles;
using FinanceTracker.Client.Layout;
using FinanceTracker.Contracts.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.Client.Layout;

public class NavMenuTests : ComponentTestBase
{
    private readonly BunitAuthorizationContext _authorization;

    public NavMenuTests()
    {
        _authorization = AddAuthorization();
        _authorization.SetAuthorized("someone");
    }

    [Test]
    public async Task Render_GroupsThePagesInOrder()
    {
        // Act
        IRenderedComponent<NavMenu> cut = RenderMenu(transfers: 1, subscriptions: 1);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Headings(cut)).IsEquivalentTo(["Money", "Planning", "Insights", "To review", "Manage"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(LinksUnder(cut, "Insights")).IsEquivalentTo(["NetWorth", "Reports"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(LinksUnder(cut, "Manage"))
                .IsEquivalentTo(["ProviderSetup", "HouseholdMembers", "Classifications", "Rules"],
                    TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task Render_NothingToReview_HidesTheReviewGroup()
    {
        // Act
        IRenderedComponent<NavMenu> cut = RenderMenu(transfers: 0, subscriptions: 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Headings(cut)).DoesNotContain("To review");
            await Assert.That(cut.FindAll($"a[href='{NavMenu.TransfersReviewPath}']")).IsEmpty();
            await Assert.That(cut.FindAll($"a[href='{NavMenu.SubscriptionsReviewPath}']")).IsEmpty();
        }
    }

    [Test]
    public async Task Render_OnlySubscriptionsToReview_ShowsOnlyThemWithTheirCount()
    {
        // Act
        IRenderedComponent<NavMenu> cut = RenderMenu(transfers: 0, subscriptions: 4);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(LinksUnder(cut, "To review")).IsEquivalentTo([NavMenu.SubscriptionsReviewPath]);
            await Assert.That(cut.Find($"a[href='{NavMenu.SubscriptionsReviewPath}'] .ft-nav__count").TextContent.Trim())
                .IsEqualTo("4");
        }
    }

    [Test]
    [Arguments("Review")]
    [Arguments("review?from=dashboard")]
    public async Task Render_OnTheReviewPageWithNothingLeft_KeepsItsLink(string path)
    {
        // Arrange
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        // Act
        IRenderedComponent<NavMenu> cut = RenderMenu(transfers: 0, subscriptions: 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(LinksUnder(cut, "To review")).IsEquivalentTo([NavMenu.TransfersReviewPath]);
            await Assert.That(cut.FindAll(".ft-nav__count")).IsEmpty();
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Render_Settings_OnlyForAdmins(bool admin)
    {
        // Arrange
        if (admin)
        {
            _authorization.SetRoles(FinTrackRoles.Admin);
        }

        // Act
        IRenderedComponent<NavMenu> cut = RenderMenu(transfers: 0, subscriptions: 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Headings(cut).Contains("Settings")).IsEqualTo(admin);
            await Assert.That(cut.FindAll("a[href='Automation']").Count).IsEqualTo(admin ? 1 : 0);
        }
    }

    private IRenderedComponent<NavMenu> RenderMenu(int transfers, int subscriptions) =>
        Render<NavMenu>(p => p
            .Add(x => x.TransfersToReview, transfers)
            .Add(x => x.SubscriptionsToReview, subscriptions));

    private static List<string> Headings(IRenderedComponent<NavMenu> cut) =>
        cut.FindAll(".ft-nav__heading").Select(static x => x.TextContent.Trim()).ToList();

    /// <summary>
    /// The links between the heading <paramref name="heading"/> and the next one.
    /// </summary>
    private static List<string> LinksUnder(IRenderedComponent<NavMenu> cut, string heading)
    {
        List<string> links = [];
        bool under = false;
        foreach (AngleSharp.Dom.IElement element in cut.FindAll(".ft-nav__heading, a.mud-nav-link"))
        {
            if (element.ClassList.Contains("ft-nav__heading"))
            {
                under = element.TextContent.Trim() == heading;
                continue;
            }

            if (under)
            {
                links.Add(element.GetAttribute("href") ?? string.Empty);
            }
        }

        return links;
    }
}
