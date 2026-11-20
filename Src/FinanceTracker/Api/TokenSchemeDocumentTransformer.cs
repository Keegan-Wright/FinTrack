using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FinanceTracker.Api;

/// <summary>
/// Describes signing in with a personal access token, sent as a bearer token, so tools such as Scalar or Postman know
/// how to call the API. Which operations it works on is said by <see cref="TokenSecurityOperationTransformer"/>.
/// </summary>
internal sealed class TokenSchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// The scheme's name in the document.
    /// </summary>
    public const string SchemeName = "ApiToken";

    /// <inheritdoc/>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "An API token, made on the API tokens page, sent as Authorization: Bearer ftk_... It only " +
                          "reads, and only the operations that list it."
        };
        return Task.CompletedTask;
    }
}
