using FinanceTracker.Middleware;

namespace FinanceTracker.Extensions;

public static class SecurityHeaderExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// The per response nonce the content security policy and the app's inline scripts share, see
        /// <see cref="ContentSecurityNonce"/>.
        /// </summary>
        public IServiceCollection AddSecurityHeaders() => services.AddScoped<ContentSecurityNonce>();
    }

    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the browser security headers to every response, see <see cref="SecurityHeadersMiddleware"/>.
        /// </summary>
        public IApplicationBuilder UseSecurityHeaders() => app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
