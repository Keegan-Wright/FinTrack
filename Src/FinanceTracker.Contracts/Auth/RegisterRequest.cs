using System.ComponentModel;

namespace FinanceTracker.Contracts.Auth;

/// <summary>
/// The registration form. Validated by <see cref="Validation.RegisterRequestValidator"/>.
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// The new account's username.
    /// </summary>
    [Description("Username for the new account")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The new account's password.
    /// </summary>
    [Description("Password for the new account")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// The new account's email address, which must be unique.
    /// </summary>
    [Description("Email address for the new account")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The user's first name.
    /// </summary>
    [Description("First name of the user")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// The user's last name.
    /// </summary>
    [Description("Last name of the user")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// The password again, must match <see cref="Password"/>.
    /// </summary>
    [Description("Confirmation of the password to ensure accuracy")]
    public string ConfirmPassword { get; set; }  = string.Empty;
}
