using Microsoft.AspNetCore.Authorization;

namespace FinanceTracker.Tokens;

/// <summary>
/// Lets a request signed in with a personal access token through only to an endpoint marked
/// <see cref="TokenAccessExtensions.ReadableWithToken"/>, and only to read it. That's a GET or HEAD, or a POST marked
/// <see cref="TokenAccessExtensions.ReadsOnly"/>. Requests signed in any other way pass.
/// </summary>
public sealed class TokenAccessHandler : AuthorizationHandler<TokenAccessRequirement, HttpContext>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        TokenAccessRequirement requirement, HttpContext resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);
        if (!context.User.HasClaim(static x => x.Type == PersonalAccessTokenDefaults.ScopeClaim))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (IsTokenRead(resource))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether <paramref name="request"/> reads an endpoint opened to tokens, see <see cref="TokenReads"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    private static bool IsTokenRead(HttpContext request) =>
        TokenReads.Allows(request.Request.Method, request.GetEndpoint()?.Metadata ?? EndpointMetadataCollection.Empty);
}
