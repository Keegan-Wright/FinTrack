using FinanceTracker.Contracts.Api;

namespace FinanceTracker.Middleware;

/// <summary>
/// Which requests are limited in how often, see <see cref="RateLimitedAction"/>.
/// </summary>
public static class RateLimitedActions
{
    private static readonly PathString[] SignInForms =
    [
        "/Account/Login", "/Account/Register", "/Account/ForgotPassword", "/Account/ResetPassword"
    ];

    /// <summary>
    /// What <paramref name="method"/> to <paramref name="path"/> does that's limited, null when it isn't.
    /// </summary>
    public static RateLimitedAction? Of(string method, PathString path)
    {
        if (!HttpMethods.IsPost(method))
        {
            return null;
        }

        if (Array.Exists(SignInForms, x => path.Equals(x, StringComparison.OrdinalIgnoreCase)))
        {
            return RateLimitedAction.SignIn;
        }

        if (path.Equals($"/{ApiRoutes.SharingLinks}", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitedAction.Invite;
        }

        return null;
    }
}
