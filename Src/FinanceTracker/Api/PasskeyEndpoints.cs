using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Api;

/// <summary>
/// Maps the passkeys API.
/// </summary>
public static class PasskeyEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IPasskeyService"/> at <see cref="ApiRoutes.Passkeys"/>. Anything for an account without a
        /// password answers 409. Adding a passkey takes two posts, the options then the browser's credential, as
        /// Identity keeps what it asked for in a cookie between them.
        /// </summary>
        public RouteGroupBuilder MapPasskeyEndpoints()
        {
            RouteGroupBuilder passkeys = api.MapGroup(ApiRoutes.Passkeys);

            passkeys.MapGet("", (IPasskeyService service, CancellationToken cancellationToken) =>
                    service.GetPasskeysAsync(cancellationToken))
                .ConflictWhenNone();

            passkeys.MapPost("options", (ConfirmPassword request, IPasskeyService service,
                    CancellationToken cancellationToken) => service.StartAddingAsync(request, cancellationToken))
                .Validated<ConfirmPassword>()
                .ConflictWhenNone();

            passkeys.MapPost("", (AddPasskey request, IPasskeyService service,
                    CancellationToken cancellationToken) => service.AddAsync(request, cancellationToken))
                .Validated<AddPasskey>()
                .ConflictWhenNone();

            passkeys.MapPut("{id}", (string id, RenamePasskey request, IPasskeyService service,
                    CancellationToken cancellationToken) => service.RenameAsync(id, request, cancellationToken))
                .Validated<RenamePasskey>()
                .ConflictWhenNone();

            passkeys.MapPost("{id}/remove", (string id, ConfirmPassword request, IPasskeyService service,
                    CancellationToken cancellationToken) => service.RemoveAsync(id, request, cancellationToken))
                .Validated<ConfirmPassword>()
                .ConflictWhenNone();

            return api;
        }
    }
}
