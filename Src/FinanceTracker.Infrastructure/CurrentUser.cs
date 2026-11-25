using System.Security.Claims;
using FinanceTracker.Infrastructure.Common;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// Scoped holder for <see cref="ICurrentUser"/>. Whatever creates the scope sets the user: middleware for requests,
/// a circuit handler for interactive circuits, <see cref="CurrentUserScopeExtensions.CreateUserScope"/> for jobs.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private Guid? _userId;

    /// <summary>
    /// The user, null when nobody is set, such as a request from someone not signed in.
    /// </summary>
    public Guid? UserIdIfSet => _userId;

    /// <inheritdoc/>
    public Guid UserId => _userId ?? throw new InvalidOperationException(
        "No user is set for this scope. Services need a signed in user, or a scope created for a user.");

    /// <summary>
    /// Acts as <paramref name="principal"/>, or as nobody when it isn't a signed in user.
    /// </summary>
    /// <param name="principal">Who's signed in, null for nobody.</param>
    public void Set(ClaimsPrincipal? principal) =>
        _userId = Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId)
            ? userId
            : null;

    /// <summary>
    /// Acts as <paramref name="userId"/>, e.g. for a background job's work for one user.
    /// </summary>
    /// <param name="userId">The user.</param>
    public void Set(Guid userId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        _userId = userId;
    }
}
