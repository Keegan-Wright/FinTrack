using System.ComponentModel;

namespace FinanceTracker.Contracts.Auth;

/// <summary>
/// The login form. Validated by <see cref="Validation.LoginRequestValidator"/>.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// The user's username or email address.
    /// </summary>
    [Description("Username or email for authentication")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The user's password.
    /// </summary>
    [Description("User's password for authentication")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Keeps the user signed in after the browser closes.
    /// </summary>
    [Description("Remember me?")]
    public bool RememberMe { get; set; }
}
