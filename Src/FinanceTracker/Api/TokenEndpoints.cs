using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Tokens;

namespace FinanceTracker.Api;

/// <summary>
/// Maps the personal access tokens API.
/// </summary>
public static class TokenEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="ITokensService"/> at <see cref="ApiRoutes.Tokens"/>, never open to a token itself. Listing or
        /// making tokens for an account without a password answers 409, revoking one the user hasn't 404.
        /// </summary>
        public RouteGroupBuilder MapTokenEndpoints()
        {
            RouteGroupBuilder tokens = api.MapGroup(ApiRoutes.Tokens);

            tokens.MapGet(string.Empty, (ITokensService service, CancellationToken cancellationToken) =>
                    service.GetTokensAsync(cancellationToken))
                .ConflictWhenNone();

            tokens.MapPost(string.Empty, (CreateToken request, ITokensService service,
                    CancellationToken cancellationToken) => service.CreateAsync(request, cancellationToken))
                .Validated<CreateToken>()
                .ConflictWhenNone();

            tokens.MapDelete("{id:guid}", (Guid id, ITokensService service, CancellationToken cancellationToken) =>
                    service.RevokeAsync(id, cancellationToken))
                .NotFoundWhenNone();

            return api;
        }
    }
}
