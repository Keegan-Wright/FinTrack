using System.ComponentModel;

namespace FinanceTracker.Models.Response.OpenBanking;

public class AuthUrlResponse
{
    [Description("URL for authenticating with the banking provider")]
    public string AuthUrl { get; set; }
}