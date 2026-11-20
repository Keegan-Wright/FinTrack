namespace FinanceTracker.Infrastructure.Account.Tokens;

/// <summary>
/// A new personal access token, see <see cref="PersonalAccessTokenFormat.Issue"/>.
/// </summary>
/// <param name="Token">The whole token, shown to the user once and never kept.</param>
/// <param name="LookupId">Its public part, kept to find it by.</param>
/// <param name="SecretHash">The SHA-256 hash of its secret part, kept to check it against.</param>
public sealed record IssuedToken(string Token, string LookupId, byte[] SecretHash);
