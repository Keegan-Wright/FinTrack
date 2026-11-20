namespace FinanceTracker.Api;

/// <summary>
/// The API's OpenAPI document, described from the code's doc comments, the request and response types' from
/// <c>Contracts</c>.
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>
    /// Where the document is served, <c>openapi/v1.json</c> by default.
    /// </summary>
    public const string DocumentPath = "/openapi/{documentName}.json";

    /// <summary>
    /// The document's name.
    /// </summary>
    public const string DocumentName = "v1";

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Describes the API's endpoints, those under <c>api/</c>, and which an API token can read. The sign in pages
        /// and the sync hub are left out.
        /// </summary>
        public IServiceCollection AddFinTrackOpenApi()
        {
            // A literal name, so the source generator adds the doc comments
            services.AddOpenApi("v1", static options =>
            {
                options.ShouldInclude = static description =>
                    description.RelativePath?.StartsWith("api/", StringComparison.Ordinal) == true;
                options.AddOperationTransformer<NoneAnswerOperationTransformer>();
                options.AddDocumentTransformer<TokenSchemeDocumentTransformer>();
                options.AddOperationTransformer<TokenSecurityOperationTransformer>();
            });
            return services;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Serves the document to signed in users, at <see cref="DocumentPath"/>. Signed out, it answers 401 like the
        /// API does, rather than sending them to sign in.
        /// </summary>
        public IEndpointRouteBuilder MapFinTrackOpenApi()
        {
            endpoints.MapOpenApi(DocumentPath)
                .RequireAuthorization()
                .DisableCookieRedirect();
            return endpoints;
        }
    }
}
