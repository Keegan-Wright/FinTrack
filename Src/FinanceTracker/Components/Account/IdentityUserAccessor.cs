using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Loads the signed in user for the account pages.
/// </summary>
/// <param name="userManager">Finds the user.</param>
/// <param name="redirectManager">Redirects when they can't be found.</param>
internal sealed class IdentityUserAccessor(
    UserManager<FinanceTrackerUser> userManager,
    IdentityRedirectManager redirectManager)
{
    /// <summary>
    /// The signed in user. Redirects to the invalid user page when they can't be found.
    /// </summary>
    /// <param name="context">The request.</param>
    public async Task<FinanceTrackerUser> GetRequiredUserAsync(HttpContext context)
    {
        FinanceTrackerUser? user = await userManager.GetUserAsync(context.User);

        if (user is null)
        {
            redirectManager.RedirectToWithStatus("Account/InvalidUser",
                $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.", context);
        }

        return user;
    }
}
