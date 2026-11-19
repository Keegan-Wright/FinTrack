namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// Someone signed in at <see cref="FakeOidcProvider"/>, as its tokens and user info describe them.
/// </summary>
/// <param name="Subject">Their id at the provider, which stays the same whatever else changes.</param>
/// <param name="EmailVerified">Whether the provider checked the email is theirs.</param>
/// <param name="Groups">The provider's groups they're in, none sends no groups claim.</param>
public sealed record FakeOidcUser(
    string Subject,
    string Email,
    bool EmailVerified,
    string PreferredUsername,
    string GivenName = "Single",
    string FamilyName = "Sign-On",
    string[]? Groups = null)
{
    /// <summary>
    /// The standard OIDC claims for them, as both the identity token and the user info endpoint send them, with their
    /// groups as an array when they have some.
    /// </summary>
    public Dictionary<string, object> Claims()
    {
        Dictionary<string, object> claims = new()
        {
            ["sub"] = Subject,
            ["email"] = Email,
            ["email_verified"] = EmailVerified,
            ["preferred_username"] = PreferredUsername,
            ["given_name"] = GivenName,
            ["family_name"] = FamilyName
        };

        if (Groups is not null)
        {
            claims["groups"] = Groups;
        }

        return claims;
    }
}
