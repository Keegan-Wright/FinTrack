namespace FinanceTracker.Api;

/// <summary>
/// Filters the endpoints share.
/// </summary>
public static class EndpointConventionExtensions
{
    extension(RouteHandlerBuilder builder)
    {
        /// <summary>
        /// Validates the <typeparamref name="TBody"/> body first, see <see cref="ValidateBodyFilter{TBody}"/>.
        /// </summary>
        public RouteHandlerBuilder Validated<TBody>() => builder.AddEndpointFilter<ValidateBodyFilter<TBody>>();

        /// <summary>
        /// Maps the service's <see cref="Contracts.NameTakenException"/> to 409, see
        /// <see cref="ConflictOnNameTakenFilter"/>.
        /// </summary>
        public RouteHandlerBuilder ConflictWhenNameTaken() =>
            builder.AddEndpointFilter<ConflictOnNameTakenFilter>();

        /// <summary>
        /// Answers 409 when the service answers null or false, as when there's nothing to do it to. True is 204. See
        /// <see cref="NoneAnswerFilter"/>.
        /// </summary>
        public RouteHandlerBuilder ConflictWhenNone() => builder.WhenNone(StatusCodes.Status409Conflict);

        /// <summary>
        /// Answers 404 when the service answers null or false, as when the user has nothing by that id. True is 204.
        /// See <see cref="NoneAnswerFilter"/>.
        /// </summary>
        public RouteHandlerBuilder NotFoundWhenNone() => builder.WhenNone(StatusCodes.Status404NotFound);

        /// <summary>
        /// Answers <paramref name="statusCode"/> for a null or false answer, and says so in the OpenAPI document.
        /// </summary>
        /// <param name="statusCode">The status for a null or false answer.</param>
        private RouteHandlerBuilder WhenNone(int statusCode) =>
            builder.AddEndpointFilter(new NoneAnswerFilter(statusCode))
                .WithMetadata(new NoneAnswerMetadata())
                .Produces(statusCode);
    }
}
