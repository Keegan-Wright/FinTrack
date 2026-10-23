using System.Security.Claims;
using FinanceTracker.Contracts.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace FinanceTracker.Components.Account;

/// <summary>
/// What the browser learns about the signed in user, handed over with each prerendered page. Only the claims the
/// pages use, the cookie's other claims (e.g. the security stamp) stay on the server.
/// </summary>
internal static class BrowserAuthenticationState
{
    private static readonly HashSet<string> BrowserClaimTypes =
        [ClaimTypes.NameIdentifier, ClaimTypes.Name, ClaimTypes.Role, CustomClaims.FirstName];

    public static ValueTask<AuthenticationStateData?> SerializeAsync(AuthenticationState state)
    {
        ClaimsPrincipal user = state.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return ValueTask.FromResult<AuthenticationStateData?>(null);
        }

        AuthenticationStateData data = new();
        foreach (Claim claim in user.Claims.Where(static x => BrowserClaimTypes.Contains(x.Type)))
        {
            data.Claims.Add(new ClaimData(claim));
        }

        return ValueTask.FromResult<AuthenticationStateData?>(data);
    }
}
