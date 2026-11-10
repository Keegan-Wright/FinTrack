using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Identity refused to save a change to an account, such as when someone else changed it at the same time.
/// </summary>
public sealed class AccountUpdateFailedException : Exception
{
    public AccountUpdateFailedException(IdentityResult result)
        : base($"Updating the account failed: {string.Join(", ", result.Errors.Select(static x => x.Description))}")
    {
    }
}
