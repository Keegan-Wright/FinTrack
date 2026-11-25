using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace FinanceTracker.Tokens;

/// <summary>
/// Lets a request signed in with a personal access token through only where its scope may go. A token reading the
/// API only reaches an endpoint marked <see cref="TokenAccessExtensions.ReadableWithToken"/>, and only to read it,
/// that's a GET or HEAD, or a POST marked <see cref="TokenAccessExtensions.ReadsOnly"/>. A token of any other scope
/// only reaches the endpoints marked <see cref="TokenAccessExtensions.UsableWithToken"/> for that scope. Requests
/// signed in any other way pass.
/// </summary>
public sealed class TokenAccessHandler : AuthorizationHandler<TokenAccessRequirement, HttpContext>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        TokenAccessRequirement requirement, HttpContext resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);
        string? scope = context.User.FindFirst(PersonalAccessTokenDefaults.ScopeClaim)?.Value;
        if (scope is null || Allows(scope, resource))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether a token of <paramref name="scope"/> may make <paramref name="request"/>.
    /// </summary>
    /// <param name="scope">The token's scope, as its claim holds it.</param>
    /// <param name="request">The request.</param>
    private static bool Allows(string scope, HttpContext request)
    {
        EndpointMetadataCollection metadata = request.GetEndpoint()?.Metadata ?? EndpointMetadataCollection.Empty;
        if (scope == nameof(TokenScope.Read))
        {
            return TokenReads.Allows(request.Request.Method, metadata);
        }

        return metadata.OfType<TokenScopeMetadata>().Any(x => x.Scope.ToString() == scope);
    }
}
