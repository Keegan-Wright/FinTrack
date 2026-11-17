using FinanceTracker.Domain.Notifications;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// What follows a change in a link, see <see cref="SharingService"/>. Telling the other person, matching transfers and
/// running rules again as reach changes, and taking off what each put on the other's transactions.
/// </summary>
/// <summary>
/// What follows a change to what's shared, see <see cref="SharingService"/>. Telling the people involved, running rules
/// again and matching transfers again.
/// </summary>
public interface ISharingEffects
{
    /// <summary>
    /// Raises the notification for <paramref name="userId"/>, acting as them so it follows their own settings.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="kind">What it's about.</param>
    /// <param name="key">Which event it is, so it's raised once.</param>
    /// <param name="title">Its heading.</param>
    /// <param name="message">What it says.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task NotifyAsync(Guid userId, NotificationKind kind, string key, string title, string message,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tells the other person in <paramref name="link"/> what the user did.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="link">The link between the two people.</param>
    /// <param name="key">Which event it is, so it's raised once.</param>
    /// <param name="title">The title, given the user's name.</param>
    /// <param name="message">What it says.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <param name="kind">What it's about.</param>
    Task NotifyOtherAsync(FinanceTrackerContext context, DomainLink link, string key,
        Func<string, string> title, string message, CancellationToken cancellationToken,
        NotificationKind kind = NotificationKind.JointAccount);

    /// <summary>
    /// Runs the rules again over <paramref name="accounts"/>, each as its owner, once who can change them changed.
    /// Rules that no longer reach them take back what they gave, and those that now do run.
    /// </summary>
    /// <param name="accounts">The accounts whose rules to run again, each with its owner.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task RerunRulesAsync(List<(Guid OwnerId, Guid AccountId)> accounts,
        CancellationToken cancellationToken);

    /// <summary>
    /// Matches both people's transfers again once what's shared between them changed. Money moved between their
    /// accounts is a transfer only while one of the two accounts is shared, so pairs come and go with the shares.
    /// </summary>
    /// <param name="context">The context to work through.</param>
    /// <param name="link">The link between the two people.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task RematchTransfersAsync(FinanceTrackerContext context, DomainLink link,
        CancellationToken cancellationToken);
}
