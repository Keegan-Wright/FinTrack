using FinanceTracker.Middleware;

namespace FinanceTracker.Extensions;

public static class SecurityHeaderExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// The per response nonce the content security policy and the app's inline scripts share, see
        /// <see cref="ContentSecurityNonce"/>, and how long browsers keep to https once they've seen the site.
        /// </summary>
        public IServiceCollection AddSecurityHeaders()
        {
            services.AddScoped<ContentSecurityNonce>();

            // A year, as the site is only ever served over https. Not its subdomains, which may be other apps on the
            // same host and are theirs to decide
            services.AddHsts(static options => options.MaxAge = TimeSpan.FromDays(365));

            return services;
        }
    }

    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the browser security headers to every response, see <see cref="SecurityHeadersMiddleware"/>.
        /// </summary>
        public IApplicationBuilder UseSecurityHeaders() => app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
