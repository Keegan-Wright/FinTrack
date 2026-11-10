using System.Text;
using FinanceTracker.Components.Account.Pages;
using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Makes the one-time links that let a user choose a new password, see <see cref="ResetPassword"/> emailed from
/// Forgot password, or handed over by an admin when FinTrack can't email them.
/// </summary>
public sealed class PasswordReset : IPasswordReset
{
    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly IAccountUrls _accountUrls;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lifespan;

    public PasswordReset(UserManager<FinanceTrackerUser> userManager, IAccountUrls accountUrls,
        TimeProvider timeProvider, IOptions<DataProtectionTokenProviderOptions> tokenOptions)
    {
        _userManager = userManager;
        _accountUrls = accountUrls;
        _timeProvider = timeProvider;
        _lifespan = tokenOptions.Value.TokenLifespan;
    }

    /// <inheritdoc/>
    public DateTime ExpiresAt => _timeProvider.GetUtcNow().UtcDateTime + _lifespan;

    /// <inheritdoc/>
    public async Task<Uri> CreateLinkAsync(FinanceTrackerUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        string code = await _userManager.GeneratePasswordResetTokenAsync(user);
        return _accountUrls.Create(ResetPassword.PagePath, new Dictionary<string, string?>
        {
            ["userId"] = user.Id.ToString(),
            ["code"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code))
        });
    }
}
