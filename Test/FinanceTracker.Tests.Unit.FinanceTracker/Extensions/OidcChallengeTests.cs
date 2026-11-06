using FinanceTracker.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

/// <summary>
/// Starting an OIDC sign in, which the provider can refuse before the user ever gets there.
/// </summary>
public class OidcChallengeTests
{
    private readonly IAuthenticationService _authentication = Substitute.For<IAuthenticationService>();

    public static IEnumerable<Func<Exception>> ProviderFailures() =>
    [
        // This site's address isn't one the provider knows, as on a machine it wasn't set up for
        () => new OpenIdConnectProtocolException("The 'redirect_uri' parameter does not match"),
        // Its settings couldn't be read
        () => new InvalidOperationException("Unable to obtain configuration"),
        // It can't be reached
        () => new HttpRequestException("Connection refused")
    ];

    [Test]
    [MethodDataSource(nameof(ProviderFailures))]
    public async Task Challenge_ProviderRefuses_GoesToSignInSayingSingleSignOnFailed(Exception failure)
    {
        // Arrange
        _authentication.ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(),
            Arg.Any<AuthenticationProperties?>()).ThrowsAsync(failure);

        // Act
        IResult result = await AuthenticationExtensions.ChallengeOidcAsync(Context(), "/Bills");

        // Assert
        await Assert.That(result).IsTypeOf<RedirectHttpResult>();
        await Assert.That(((RedirectHttpResult)result).Url).IsEqualTo("/Account/Login?sso=failed");
    }

    [Test]
    public async Task Challenge_ProviderAccepts_SendsThemThereComingBackToWhereTheyWere()
    {
        // Act
        IResult result = await AuthenticationExtensions.ChallengeOidcAsync(Context(), "/Bills");

        // Assert, the handler has answered with the redirect to the provider
        await _authentication.Received(1).ChallengeAsync(Arg.Any<HttpContext>(), "OpenIdConnect",
            Arg.Is<AuthenticationProperties?>(x =>
                x!.RedirectUri == $"/auth/oidc/complete?returnUrl={Uri.EscapeDataString("/Bills")}"));
        await Assert.That(result).IsNotTypeOf<RedirectHttpResult>();
    }

    private DefaultHttpContext Context()
    {
        ServiceCollection services = new();
        services.AddSingleton(_authentication);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        DefaultHttpContext context = new() { RequestServices = services.BuildServiceProvider() };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("fintrack.example.test");
        return context;
    }
}
