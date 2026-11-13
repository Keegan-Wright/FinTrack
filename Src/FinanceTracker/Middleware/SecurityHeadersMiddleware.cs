namespace FinanceTracker.Middleware;

/// <summary>
/// Adds the browser security headers to every response. A content security policy limiting where scripts, styles,
/// images and connections can come from, no framing by other sites, no guessing content types, and no sending the
/// page's address to other sites.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _next = next;
        _isDevelopment = environment.IsDevelopment();
    }

    public Task InvokeAsync(HttpContext context, ContentSecurityNonce nonce)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nonce);

        // Once the answer is under way, the exception handler clears headers set before it
        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy(nonce.Value);
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            // Switches off browser features the app never uses, so nothing injected into a page can ask for them
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            return Task.CompletedTask;
        });

        return _next(context);
    }

    /// <summary>
    /// Scripts are the app's own files and its inline scripts carrying <paramref name="nonce"/>, with the runtime
    /// compiling WebAssembly. Eval is allowed as Blazor-ApexCharts turns the charts' label formatters, sent as text,
    /// back into functions with it, see ChartDefaults.MoneyFormatter. An injected inline script still can't run
    /// without the nonce, so it only ever runs what the app sends. Styles allow inline ones, as MudBlazor and the charts set them on elements. Fonts are the
    /// app's own, see css/fonts.css. Bank logos come from the banks' own sites. No form action rule, as signing in with OIDC or connecting a
    /// bank posts a form that then goes to the provider.
    /// </summary>
    private string ContentSecurityPolicy(string nonce)
    {
        // Hot reload talks to the dev server over its own socket
        string connect = _isDevelopment ? "'self' ws://localhost:* wss://localhost:* http://localhost:*" : "'self'";

        return string.Join("; ",
            "default-src 'self'",
            $"script-src 'self' 'nonce-{nonce}' 'wasm-unsafe-eval' 'unsafe-eval'",
            "style-src 'self' 'unsafe-inline'",
            "font-src 'self' data:",
            "img-src 'self' data: blob: https:",
            $"connect-src {connect}",
            "object-src 'none'",
            "base-uri 'self'",
            "frame-ancestors 'none'");
    }
}
