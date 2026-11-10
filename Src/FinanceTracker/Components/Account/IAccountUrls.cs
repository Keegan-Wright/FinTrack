namespace FinanceTracker.Components.Account;

/// <summary>
/// Builds the full URLs of account pages, such as a password reset, for the emails and links that carry them.
/// </summary>
public interface IAccountUrls
{
    /// <summary>
    /// The full URL of an account page with its query.
    /// </summary>
    /// <param name="path">The page, relative to the app, e.g. <c>Account/ConfirmEmail</c>.</param>
    /// <param name="query">What the page is given, escaped here.</param>
    /// <exception cref="InvalidOperationException">Outside a request without a public address.</exception>
    Uri Create(string path, IDictionary<string, string?> query);
}
