using FinanceTracker.Data.Models;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Components.Account;

internal sealed class IdentityUserAccessor(
    UserManager<FinanceTrackerUser> userManager,
    IdentityRedirectManager redirectManager)
{
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
