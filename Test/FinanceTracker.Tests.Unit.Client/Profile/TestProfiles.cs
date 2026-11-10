using FinanceTracker.Contracts.Profile;

namespace FinanceTracker.Tests.Unit.Client.Profile;

/// <summary>
/// A profile as the server answers it a local account with a confirmed email on a FinTrack with a mail server,
/// unless said.
/// </summary>
internal static class TestProfiles
{
    public const string Address = "sam@example.test";

    public static ProfileDetails Create(bool hasPassword = true, bool emailConfirmed = true,
        bool mailAvailable = true) => new()
    {
        Username = "sam",
        Email = Address,
        EmailConfirmed = emailConfirmed,
        HasPassword = hasPassword,
        UsesIdentityProvider = !hasPassword,
        MailAvailable = mailAvailable
    };
}
