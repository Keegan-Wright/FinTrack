using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

/// <summary>
/// Single sign on is optional, see <c>IsOidcEnabled</c>. Signing in with it end to end is covered by the
/// integration tests' <c>OidcSignInTests</c>, which run with it configured.
/// </summary>
public class OidcEndpointsTests
{
    [Test]
    public async Task MapOidcChallenge_OidcNotConfigured_MapsNothing()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Configuration[FinTrackEnvironment.OidcAuthority] = string.Empty;
        builder.Configuration[FinTrackEnvironment.OidcClientId] = string.Empty;
        builder.Configuration[FinTrackEnvironment.OidcClientSecret] = string.Empty;
        await using WebApplication app = builder.Build();

        // Act
        app.MapOidcChallenge();

        // Assert
        List<string?> routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static x => x.RoutePattern.RawText)
            .ToList();
        await Assert.That(routes.Where(static x => x?.StartsWith("/auth/oidc", StringComparison.Ordinal) == true))
            .IsEmpty();
    }
}
