namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// The signed in user's email and whether it's confirmed. Notifications and password resets are only emailed to a
/// confirmed one.
/// </summary>
public sealed class EmailStatus
{
    /// <summary>
    /// The account's email, null when it has none.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Whether the user has confirmed <see cref="Email"/> is theirs, by the link emailed to it.
    /// </summary>
    public required bool IsConfirmed { get; init; }

    /// <summary>
    /// Whether this FinTrack can send email at all, set up by whoever runs it.
    /// </summary>
    public required bool MailAvailable { get; init; }

    /// <summary>
    /// Whether the user should be asked to confirm: there's an email to confirm and a way to send the link.
    /// </summary>
    public bool NeedsConfirming => MailAvailable && Email is not null && !IsConfirmed;
}
