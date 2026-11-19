namespace FinanceTracker.Tokens;

/// <summary>
/// Opens endpoints to personal access tokens. Nothing is open unless it's marked, see <see cref="TokenAccessHandler"/>.
/// </summary>
public static class TokenAccessExtensions
{
    extension<TBuilder>(TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        /// <summary>
        /// Lets a personal access token read these endpoints, their GETs and any marked
        /// <see cref="ReadsOnly"/>. Only for the user's data, never their account or settings.
        /// </summary>
        public TBuilder ReadableWithToken() => builder.WithMetadata(new TokenReadableMetadata());

        /// <summary>
        /// Marks a POST that only reads, such as a search with its filters in the body, so a token may use it.
        /// </summary>
        public TBuilder ReadsOnly() => builder.WithMetadata(new ReadsOnlyMetadata());
    }
}
