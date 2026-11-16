using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// An in process stand in for an OIDC provider such as Authelia, the app under test is pointed at it through the
/// fixture. It serves its settings and signing key, and answers the token and user info endpoints for each sign in a
/// test <see cref="Approve">approves</see>. The user's part at the provider, signing in and agreeing, is the test
/// posting the approval back to the app as the browser would.
/// </summary>
public sealed class FakeOidcProvider : IDisposable
{
    public const string ClientId = "fintrack-integration-tests";
    public const string ClientSecret = "fake-oidc-secret";

    private const string KeyId = "fake-oidc-key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RSA _signingKey = RSA.Create(2048);

    public FakeOidcProvider()
    {
        RSAParameters key = _signingKey.ExportParameters(false);

        Server
            .Given(Request.Create().WithPath("/.well-known/openid-configuration").UsingGet())
            .RespondWith(Json(new Dictionary<string, object>
            {
                ["issuer"] = Issuer,
                ["authorization_endpoint"] = $"{Issuer}/authorize",
                ["token_endpoint"] = $"{Issuer}/token",
                ["userinfo_endpoint"] = $"{Issuer}/userinfo",
                ["jwks_uri"] = $"{Issuer}/jwks",
                ["response_types_supported"] = new[] { "code" },
                ["subject_types_supported"] = new[] { "public" },
                ["id_token_signing_alg_values_supported"] = new[] { "RS256" }
            }));

        Server
            .Given(Request.Create().WithPath("/jwks").UsingGet())
            .RespondWith(Json(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA",
                        use = "sig",
                        alg = "RS256",
                        kid = KeyId,
                        n = Base64Url(key.Modulus!),
                        e = Base64Url(key.Exponent!)
                    }
                }
            }));
    }

    public WireMockServer Server { get; } = WireMockServer.Start();

    /// <summary>
    /// The provider's address, the app's <c>OIDC_Authority</c>, and the issuer of its tokens.
    /// </summary>
    public string Issuer => Server.Url!;

    /// <summary>
    /// Lets the app exchange <paramref name="code"/> for a signed in <paramref name="user"/>, as the provider does once
    /// someone has signed in and agreed.
    /// </summary>
    /// <param name="nonce">The nonce the app sent with the sign in, which the identity token has to carry back.</param>
    public void Approve(string code, string nonce, FakeOidcUser user)
    {
        string accessToken = $"access-{code}";

        Server
            .Given(Request.Create()
                .WithPath("/token")
                .UsingPost()
                .WithBody(new RegexMatcher($"(^|&)code={code}(&|$)")))
            .RespondWith(Json(new
            {
                id_token = IdentityToken(nonce, user),
                access_token = accessToken,
                token_type = "Bearer",
                expires_in = 300
            }));

        Server
            .Given(Request.Create()
                .WithPath("/userinfo")
                .UsingGet()
                .WithHeader("Authorization", $"Bearer {accessToken}"))
            .RespondWith(Json(user.Claims()));
    }

    public void Dispose()
    {
        Server.Stop();
        _signingKey.Dispose();
    }

    /// <summary>
    /// The identity token for <paramref name="user"/>, signed with the key the provider publishes.
    /// </summary>
    private string IdentityToken(string nonce, FakeOidcUser user)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Dictionary<string, object> claims = user.Claims();
        claims["iss"] = Issuer;
        claims["aud"] = ClientId;
        claims["nonce"] = nonce;
        claims["iat"] = now;
        claims["exp"] = now + 300;

        string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT", kid = KeyId }));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        byte[] signature = _signingKey.SignData(Encoding.ASCII.GetBytes($"{header}.{payload}"),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{header}.{payload}.{Base64Url(signature)}";
    }

    private static IResponseBuilder Json(object body) =>
        Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(JsonSerializer.Serialize(body, JsonOptions));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
