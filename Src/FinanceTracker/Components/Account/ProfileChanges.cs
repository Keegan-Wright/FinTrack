using FinanceTracker.Contracts.Profile;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Identity's results as a <see cref="ProfileChange"/>, its errors in FinTrack's words where they differ.
/// </summary>
internal static class ProfileChanges
{
    public static ProfileChange From(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return new ProfileChange();
        }

        return new ProfileChange { Errors = result.Errors.Select(Describe).ToList() };
    }

    private static string Describe(IdentityError error)
    {
        if (error.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
        {
            return "Your current password isn't right";
        }

        return error.Description;
    }
}
