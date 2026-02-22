using System.ComponentModel;

namespace FinanceTracker.Models.Request.Auth;

public class LoginRequest
{
    [Description("Username or email for authentication")]
    public string Username { get; set; } = string.Empty;

    [Description("User's password for authentication")]
    public string Password { get; set; } = string.Empty;

    [Description("Remember me?")]
    public bool RememberMe { get; set; }
}
