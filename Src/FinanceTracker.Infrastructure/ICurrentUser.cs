using System.Security.Claims;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// The user the current scope acts for. Services read their user from here rather than from an
/// <see cref="ClaimsPrincipal"/>, so the same services work for requests, interactive circuits and background jobs.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// The user's id.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing has set a user for this scope.</exception>
    Guid UserId { get; }
}
