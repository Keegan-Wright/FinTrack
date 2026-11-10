using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Components.Account;

/// <summary>
/// <see cref="IProfileService"/> over Identity's <see cref="UserManager{TUser}"/>, for the signed in user.
/// </summary>
public sealed class ProfileService : IProfileService
{
    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly ICurrentUser _currentUser;
    private readonly IMailSender _mailSender;
    private readonly EmailConfirmation _emailConfirmation;

    public ProfileService(UserManager<FinanceTrackerUser> userManager, ICurrentUser currentUser,
        IMailSender mailSender, EmailConfirmation emailConfirmation)
    {
        _userManager = userManager;
        _currentUser = currentUser;
        _mailSender = mailSender;
        _emailConfirmation = emailConfirmation;
    }

    public async Task<EmailStatus> GetEmailStatusAsync(CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await UserAsync();
        string? email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email;
        return new EmailStatus
        {
            Email = email,
            IsConfirmed = user.EmailConfirmed,
            MailAvailable = _mailSender.IsAvailable
        };
    }

    public async Task<bool> SendEmailConfirmationAsync(CancellationToken cancellationToken) =>
        await _emailConfirmation.SendAsync(await UserAsync());

    private async Task<FinanceTrackerUser> UserAsync() =>
        await _userManager.FindByIdAsync(_currentUser.UserId.ToString())
        ?? throw new InvalidOperationException("The signed in user no longer exists");
}
