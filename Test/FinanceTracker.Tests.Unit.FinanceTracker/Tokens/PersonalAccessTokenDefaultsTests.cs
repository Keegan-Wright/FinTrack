using FinanceTracker.Tokens;
using Microsoft.AspNetCore.Http;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Tokens;

public class PersonalAccessTokenDefaultsTests
{
    [Test]
    [Arguments("/api/accounts", "Bearer ftk_0123456789abcdef_secret", true)]
    [Arguments("/API/accounts", "bearer ftk_0123456789abcdef_secret", true)]
    [Arguments("/YourProfile", "Bearer ftk_0123456789abcdef_secret", false)]
    [Arguments("/apiary", "Bearer ftk_0123456789abcdef_secret", false)]
    [Arguments("/api/accounts", "Bearer someone-elses-token", false)]
    [Arguments("/api/accounts", "Basic ftk_0123456789abcdef_secret", false)]
    [Arguments("/api/accounts", null, false)]
    public async Task IsTokenRequest_OnlyForTheApiWithAFinTrackBearerToken(string path, string? authorization,
        bool expected)
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = path;
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        // Act & Assert
        await Assert.That(PersonalAccessTokenDefaults.IsTokenRequest(context.Request)).IsEqualTo(expected);
    }
}
