using FinanceTracker.Api;
using FinanceTracker.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Api;

/// <summary>
/// The OpenAPI document as the app builds it:. The API's endpoints and nothing else, described from the doc comments,
/// and only for a signed in user.
/// </summary>
public class OpenApiDocumentTests
{
    [Test]
    public async Task Document_DescribesTheApi_AndNothingElse()
    {
        // Arrange
        await using WebApplication app = await StartAppAsync();

        // Act
        OpenApiDocument document = await DocumentAsync(app);
        List<string> paths = document.Paths.Keys.ToList();

        // Assert
        using (Assert.Multiple())
        {
            // The API's ~120 endpoints share fewer paths, one per address
            await Assert.That(paths.Count).IsGreaterThan(50);
            await Assert.That(paths.Where(static x => !x.StartsWith("/api/", StringComparison.Ordinal))).IsEmpty();
            await Assert.That(paths.Where(static x => x.Contains("**", StringComparison.Ordinal))).IsEmpty();
        }
    }

    [Test]
    public async Task Document_DescribesContractsFromTheirDocComments()
    {
        // Arrange
        await using WebApplication app = await StartAppAsync();

        // Act
        OpenApiDocument document = await DocumentAsync(app);
        IOpenApiSchema saveBill = document.Components!.Schemas!["SaveBill"];

        // Assert
        await Assert.That(saveBill.Description)
            .IsEqualTo("A new bill, or everything the user can change about an existing one.");
    }

    [Test]
    public async Task Document_AnswersTurnedIntoResponses_AreDescribedAsSent()
    {
        // Arrange
        await using WebApplication app = await StartAppAsync();

        // Act
        OpenApiDocument document = await DocumentAsync(app);
        IOpenApiPathItem goal = document.Paths["/api/savings-goals/{id}"];
        IOpenApiPathItem resetLink = document.Paths["/api/users/{id}/reset-link"];

        // Assert, a true or false answer is 204 or 404, a null one 409 beside the 200
        using (Assert.Multiple())
        {
            await Assert.That(goal.Operations![HttpMethod.Delete].Responses!.Keys).IsEquivalentTo(["204", "404"]);
            await Assert.That(resetLink.Operations![HttpMethod.Post].Responses!.Keys).IsEquivalentTo(["200", "409"]);
        }
    }

    [Test]
    public async Task DocumentEndpoint_NeedsASignedInUser()
    {
        // Arrange
        await using WebApplication app = await StartAppAsync();

        // Act
        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(static x => x.RoutePattern.RawText == OpenApiExtensions.DocumentPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(endpoint.Metadata.GetMetadata<IAllowAnonymous>()).IsNull();
            await Assert.That(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()).IsNotEmpty();
        }
    }

    /// <summary>
    /// The app with the API and its document mapped, and enough registered for the endpoints to build, as in
    /// <see cref="ApiEndpointSecurityTests"/>. Started, on a free port, as the document only sees the endpoints once
    /// routing has them. Nothing is called.
    /// </summary>
    private static async Task<WebApplication> StartAppAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
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
        builder.Services.AddFinTrackOpenApi();

        WebApplication app = builder.Build();
        app.MapFinTrackApi();
        app.MapFinTrackOpenApi();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        return app;
    }

    private static Task<OpenApiDocument> DocumentAsync(WebApplication app) =>
        app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(OpenApiExtensions.DocumentName)
            .GetOpenApiDocumentAsync();
}
