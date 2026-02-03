using System.ComponentModel;

namespace FinanceTracker.Models.Request.Auth;


public class LoginRequest
{
    [Description("Username or email for authentication")]
    public required string Username { get; set; }

    [Description("User's password for authentication")]
    public required string Password { get; set; }
}