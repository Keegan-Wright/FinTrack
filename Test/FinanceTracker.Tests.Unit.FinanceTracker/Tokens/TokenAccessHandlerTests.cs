using System.Security.Claims;
#if FINTRACK_AI
using FinanceTracker.Domain.Identity;
#endif
using FinanceTracker.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Tokens;

public class TokenAccessHandlerTests
{
    private readonly TokenAccessHandler _subject = new();

    [Test]
    public async Task SignedInWithTheCookie_Passes()
    {
        // Act
        bool passed = await PassesAsync(User(token: false), Request("PUT"));

        // Assert
        await Assert.That(passed).IsTrue();
    }

    [Test]
    [Arguments("GET")]
    [Arguments("HEAD")]
    public async Task Token_ReadingAnOpenedEndpoint_Passes(string method)
    {
        // Act
        bool passed = await PassesAsync(User(token: true), Request(method, new TokenReadableMetadata()));

        // Assert
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task Token_GettingAnEndpointNotOpened_IsRefused()
    {
        // Act
        bool passed = await PassesAsync(User(token: true), Request("GET"));

        // Assert
        await Assert.That(passed).IsFalse();
    }

    [Test]
    [Arguments("POST")]
    [Arguments("PUT")]
    [Arguments("DELETE")]
    [Arguments("PATCH")]
    public async Task Token_ChangingAnOpenedEndpoint_IsRefused(string method)
    {
        // Act
        bool passed = await PassesAsync(User(token: true), Request(method, new TokenReadableMetadata()));

        // Assert
        await Assert.That(passed).IsFalse();
    }

    [Test]
    public async Task Token_APostThatOnlyReads_Passes()
    {
        // Act
        bool passed = await PassesAsync(User(token: true),
            Request("POST", new TokenReadableMetadata(), new ReadsOnlyMetadata()));

        // Assert
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task Token_APostThatOnlyReadsOnAnEndpointNotOpened_IsRefused()
    {
        // Act
        bool passed = await PassesAsync(User(token: true), Request("POST", new ReadsOnlyMetadata()));

        // Assert
        await Assert.That(passed).IsFalse();
    }

#if FINTRACK_AI
    [Test]
    [Category("AiModule")]
    [Arguments("POST")]
    [Arguments("GET")]
    public async Task AiToken_OnItsScopesEndpoint_Passes(string method)
    {
        // Act
        bool passed = await PassesAsync(User(token: true, scope: "Ai"),
            Request(method, new TokenScopeMetadata(TokenScope.Ai)));

        // Assert
        await Assert.That(passed).IsTrue();
    }

    [Test]
    [Category("AiModule")]
    public async Task AiToken_ReadingTheApi_IsRefused()
    {
        // Act
        bool passed = await PassesAsync(User(token: true, scope: "Ai"), Request("GET", new TokenReadableMetadata()));

        // Assert
        await Assert.That(passed).IsFalse();
    }

    [Test]
    [Category("AiModule")]
    [Arguments("POST")]
    [Arguments("GET")]
    public async Task ReadToken_OnAnAiEndpoint_IsRefused(string method)
    {
        // Act
        bool passed = await PassesAsync(User(token: true), Request(method, new TokenScopeMetadata(TokenScope.Ai)));

        // Assert
        await Assert.That(passed).IsFalse();
    }

#endif
    private async Task<bool> PassesAsync(ClaimsPrincipal user, HttpContext request)
    {
        TokenAccessRequirement requirement = new();
        AuthorizationHandlerContext context = new([requirement], user, request);
        await _subject.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static ClaimsPrincipal User(bool token, string scope = "Read")
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())];
        if (token)
        {
            claims.Add(new Claim(PersonalAccessTokenDefaults.ScopeClaim, scope));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static DefaultHttpContext Request(string method, params object[] metadata)
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(metadata), "test"));
        return context;
    }
}
