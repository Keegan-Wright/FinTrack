namespace FinanceTracker.Api;

public static class EndpointConventionExtensions
{
    extension(RouteHandlerBuilder builder)
    {
        /// <summary>
        /// Validates the <typeparamref name="TBody"/> body first, see <see cref="ValidateBodyFilter{TBody}"/>.
        /// </summary>
        public RouteHandlerBuilder Validated<TBody>() => builder.AddEndpointFilter<ValidateBodyFilter<TBody>>();

        /// <summary>
        /// Maps the service's not found <see cref="InvalidOperationException"/> to 404, see
        /// <see cref="NotFoundOnInvalidOperationFilter"/>.
        /// </summary>
        public RouteHandlerBuilder NotFoundWhenMissing() =>
            builder.AddEndpointFilter<NotFoundOnInvalidOperationFilter>();

        /// <summary>
        /// Maps the service's <see cref="Contracts.NameTakenException"/> to 409, see
        /// <see cref="ConflictOnNameTakenFilter"/>.
        /// </summary>
        public RouteHandlerBuilder ConflictWhenNameTaken() =>
            builder.AddEndpointFilter<ConflictOnNameTakenFilter>();
    }
}
