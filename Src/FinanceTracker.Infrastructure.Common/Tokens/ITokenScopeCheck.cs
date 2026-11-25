namespace FinanceTracker.Infrastructure.Common.Tokens;

/// <summary>
/// A check a module adds to signing in with a personal access token of one scope, e.g. that the user still has AI
/// turned on for an AI client's token. A user it refuses isn't signed in, so the request answers 401.
/// </summary>
public interface ITokenScopeCheck
{
    /// <summary>
    /// The scope it checks tokens of, by its name, e.g. <c>Ai</c>.
    /// </summary>
    string Scope { get; }

    /// <summary>
    /// Whether <paramref name="userId"/> may sign in with a token of <see cref="Scope"/>.
    /// </summary>
    /// <param name="userId">The token's user.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<bool> AllowsAsync(Guid userId, CancellationToken cancellationToken);
}
