using System.ComponentModel;

namespace FinanceTracker.Models.Request.Auth;

public class RegisterRequest
{
    [Description("Username for the new account")]
    public required string Username { get; set; }

    [Description("Password for the new account")]
    public required string Password { get; set; }

    [Description("Email address for the new account")]
    public required string Email { get; set; }

    [Description("First name of the user")]
    public required string FirstName { get; set; }

    [Description("Last name of the user")]
    public required string LastName { get; set; }

    [Description("Confirmation of the password to ensure accuracy")]
    public required string ConfirmPassword { get; set; }
}