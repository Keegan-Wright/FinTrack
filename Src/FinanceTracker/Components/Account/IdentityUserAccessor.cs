using Microsoft.AspNetCore.Identity;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;

namespace FinanceTracker.Components.Account;

internal sealed class IdentityUserAccessor(
    UserManager<FinanceTrackerUser> userManager,
    IdentityRedirectManager redirectManager)
{
    public async Task<FinanceTrackerUser> GetRequiredUserAsync(HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);

        if (user is null)
        {
            redirectManager.RedirectToWithStatus("Account/InvalidUser",
                $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.", context);
        }

        return user;
    }
}