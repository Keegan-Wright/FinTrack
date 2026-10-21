using System.Net;
using System.Text.RegularExpressions;

namespace FinanceTracker.Tests.Integration.Http;

/// <summary>
/// An <see cref="HttpClient"/> with its own cookie  set, can submit the app's server rendered Identity forms.
/// Redirects are not followed so tests can assert on where the app sends them.
/// </summary>
public sealed partial class FinTrackClient : IDisposable
{
    private readonly HttpClient _client;

    public FinTrackClient(Uri baseAddress)
    {
        Cookies = new CookieContainer();
        _client = new HttpClient(new HttpClientHandler
        {
            CookieContainer = Cookies,
            AllowAutoRedirect = false,
            // The app is served with the local ASP.NET development certificate, which may not be trusted
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            BaseAddress = baseAddress
        };
    }

    public CookieContainer Cookies { get; }

    public Uri BaseAddress => _client.BaseAddress!;

    public Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken) =>
        _client.GetAsync(path, cancellationToken);

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption,
        CancellationToken cancellationToken) =>
        _client.SendAsync(request, completionOption, cancellationToken);

    public Task<HttpResponseMessage> RegisterAsync(string username, string password, CancellationToken cancellationToken) =>
        SubmitFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["RegisterRequest.Username"] = username,
            ["RegisterRequest.FirstName"] = "Integration",
            ["RegisterRequest.LastName"] = "Test",
            ["RegisterRequest.Email"] = $"{username}@fintrack.test",
            ["RegisterRequest.Password"] = password,
            ["RegisterRequest.ConfirmPassword"] = password
        }, cancellationToken);

    public Task<HttpResponseMessage> LoginAsync(string username, string password, CancellationToken cancellationToken) =>
        SubmitFormAsync("/Account/Login", new Dictionary<string, string>
        {
            ["LoginRequest.Username"] = username,
            ["LoginRequest.Password"] = password
        }, cancellationToken);

    /// <summary>
    /// Signs out through the logout endpoint, using an antiforgery token from the login page as the logout form
    /// only renders inside the interactive menu.
    /// </summary>
    public Task<HttpResponseMessage> LogoutAsync(CancellationToken cancellationToken) =>
        SubmitFormAsync("/Account/Login", new Dictionary<string, string> { ["ReturnUrl"] = string.Empty },
            cancellationToken, postPath: "/Account/Logout");

    /// <summary>
    /// Loads the page at <paramref name="path"/> and posts its form back with <paramref name="fields"/>, carrying
    /// over the hidden fields the page rendered (form handler and antiforgery token).
    /// </summary>
    /// <param name="postPath">Where to post the form, the page itself when omitted.</param>
    public async Task<HttpResponseMessage> SubmitFormAsync(string path, IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken, string? postPath = null)
    {
        string page = await _client.GetStringAsync(path, cancellationToken);

        Dictionary<string, string> form = HiddenInput().Matches(page)
            .ToDictionary(x => WebUtility.HtmlDecode(x.Groups["name"].Value), x => WebUtility.HtmlDecode(x.Groups["value"].Value));

        foreach ((string name, string value) in fields)
        {
            form[name] = value;
        }

        using FormUrlEncodedContent content = new(form);
        return await _client.PostAsync(postPath ?? path, content, cancellationToken);
    }

    /// <summary>
    /// Whether the client holds the Identity sign in cookie.
    /// </summary>
    public bool IsSignedIn => Cookies.GetCookies(BaseAddress).Any(x => x.Name == ".AspNetCore.Identity.Application");

    public void Dispose() => _client.Dispose();

    [GeneratedRegex(@"<input[^>]*type=""hidden""[^>]*name=""(?<name>[^""]+)""[^>]*value=""(?<value>[^""]*)""")]
    private static partial Regex HiddenInput();
}
