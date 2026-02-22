using System.ComponentModel;

namespace FinanceTracker.Models.Request.Auth;

public class RegisterRequest
{
    [Description("Username for the new account")]
    public string? Username { get; set; }

    [Description("Password for the new account")]
    public string? Password { get; set; }

    [Description("Email address for the new account")]
    public string? Email { get; set; }

    [Description("First name of the user")]
    public string? FirstName { get; set; }

    [Description("Last name of the user")]
    public string? LastName { get; set; }

    [Description("Confirmation of the password to ensure accuracy")]
    public string? ConfirmPassword { get; set; }
}
