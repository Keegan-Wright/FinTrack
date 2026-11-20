using FinanceTracker.Tokens;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FinanceTracker.Api;

/// <summary>
/// Says an API token works on an operation it can read, see <see cref="TokenReads"/>. The rest need the app's own
/// sign in, so list no scheme.
/// </summary>
internal sealed class TokenSecurityOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        if (!IsTokenRead(context) || context.Document is null)
        {
            return Task.CompletedTask;
        }

        OpenApiSecuritySchemeReference scheme = new(TokenSchemeDocumentTransformer.SchemeName, context.Document);
        operation.Security = [new OpenApiSecurityRequirement { [scheme] = [] }];
        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether the operation is one a token can read, by the same rule as <see cref="TokenAccessHandler"/>.
    /// </summary>
    /// <param name="context">The operation's context.</param>
    private static bool IsTokenRead(OpenApiOperationTransformerContext context) =>
        TokenReads.Allows(context.Description.HttpMethod ?? string.Empty,
            context.Description.ActionDescriptor.EndpointMetadata);
}
