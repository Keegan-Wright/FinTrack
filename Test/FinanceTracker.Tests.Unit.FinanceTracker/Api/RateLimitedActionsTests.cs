using FinanceTracker.Middleware;
using Microsoft.AspNetCore.Http;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Api;

public class RateLimitedActionsTests
{
    [Test]
    [Arguments("POST", "/Account/Login", RateLimitedAction.SignIn)]
    [Arguments("POST", "/account/login", RateLimitedAction.SignIn)]
    [Arguments("POST", "/Account/Register", RateLimitedAction.SignIn)]
    [Arguments("POST", "/Account/ForgotPassword", RateLimitedAction.SignIn)]
    [Arguments("POST", "/Account/ResetPassword", RateLimitedAction.SignIn)]
    [Arguments("POST", "/api/sharing/links", RateLimitedAction.Invite)]
    public async Task Of_TheLimitedRequests_SaysWhatTheyDo(string method, string path, RateLimitedAction expected) =>
        await Assert.That(RateLimitedActions.Of(method, new PathString(path))).IsEqualTo(expected);

    [Test]
    [Arguments("GET", "/Account/Login")]
    [Arguments("GET", "/api/sharing/links")]
    [Arguments("POST", "/api/sharing/links/accept")]
    [Arguments("POST", "/api/transactions")]
    [Arguments("POST", "/Account/Manage")]
    public async Task Of_AnythingElse_IsntLimited(string method, string path) =>
        await Assert.That(RateLimitedActions.Of(method, new PathString(path))).IsNull();
}
