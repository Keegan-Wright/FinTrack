using FinanceTracker.Api;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Api;

/// <summary>
/// Every endpoint the API maps, read from the app's own routing rather than a list kept by hand, so one mapped
/// outside the signed in group, or a new group, can't slip through. The integration tests check a signed out call
/// is answered 401 and another user's data isn't reachable.
/// </summary>
public class ApiEndpointSecurityTests
{
    private static readonly string[] AdminRoutes = [ApiRoutes.AutomationJobs, ApiRoutes.AutomationRuns];

    [Test]
    public async Task EveryEndpoint_NeedsASignedInUser()
    {
        // Arrange
        List<RouteEndpoint> endpoints = MapApi();

        // Act
        List<string> open = endpoints.Where(static x => !NeedsSignIn(x)).Select(Describe).ToList();

        // Assert
        using (Assert.Multiple())
        {
            // The API's ~120 endpoints and the sync hub's, so they were all read
            await Assert.That(endpoints.Count).IsGreaterThan(100);
            await Assert.That(open).IsEmpty();
        }
    }

    [Test]
    public async Task AutomationJobsAndRuns_NeedAnAdmin()
    {
        // Arrange
        List<RouteEndpoint> jobs = MapApi()
            .Where(static x => AdminRoutes.Any(route => Route(x).StartsWith(route, StringComparison.Ordinal)))
            .ToList();

        // Act
        List<string> notAdminOnly = jobs.Where(static x => !NeedsAnAdmin(x)).Select(Describe).ToList();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(jobs).IsNotEmpty();
            await Assert.That(notAdminOnly).IsEmpty();
        }
    }

    [Test]
    public async Task OnlyTheJobsAndRuns_NeedAnAdmin()
    {
        // Arrange
        List<RouteEndpoint> endpoints = MapApi();

        // Act, a user's own syncs and everything else are theirs
        List<string> adminOnly = endpoints
            .Where(static x => !AdminRoutes.Any(route => Route(x).StartsWith(route, StringComparison.Ordinal)))
            .Where(NeedsAnAdmin)
            .Select(Describe)
            .ToList();

        // Assert
        await Assert.That(adminOnly).IsEmpty();
    }

    /// <summary>
    /// The API's endpoints as the app maps them, with the services they take registered as the app does so each
    /// endpoint builds, nothing is resolved or called.
    /// </summary>
    private static List<RouteEndpoint> MapApi()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        // Only enough is registered for the endpoints to map, so the services aren't checked as they would be in
        // Development, e.g. a test run from the IDE, where every one would have to be buildable
        builder.Host.UseDefaultServiceProvider(static options =>
        {
            options.ValidateOnBuild = false;
            options.ValidateScopes = false;
        });
        Program.AddFinanceTrackerServices(builder.Services);
        Program.AddFinanceTrackerValidators(builder.Services);
        Program.AddFinanceTrackerExternalServices(builder.Services);
        builder.Services.AddFinTrackAccountServices();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddAuthorization();
        builder.Services.AddSignalR();

        WebApplication app = builder.Build();
        app.MapFinTrackApi();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static bool NeedsSignIn(RouteEndpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return false;
        }

        return endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0
               || endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Count > 0;
    }

    private static bool NeedsAnAdmin(RouteEndpoint endpoint)
    {
        bool byAttribute = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(static x => x.Roles == FinTrackRoles.Admin);
        bool byPolicy = endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>()
            .SelectMany(static x => x.Requirements)
            .OfType<RolesAuthorizationRequirement>()
            .Any(static x => x.AllowedRoles.Contains(FinTrackRoles.Admin));

        return byAttribute || byPolicy;
    }

    private static string Route(RouteEndpoint endpoint) => (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');

    private static string Describe(RouteEndpoint endpoint) => endpoint.DisplayName ?? Route(endpoint);
}
