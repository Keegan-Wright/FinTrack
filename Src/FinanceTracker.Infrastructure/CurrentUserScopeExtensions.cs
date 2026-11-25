using FinanceTracker.Infrastructure.Common;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// Registers the current user.
/// </summary>
public static class CurrentUserScopeExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="CurrentUser"/> as the scope's <see cref="ICurrentUser"/>.
        /// </summary>
        public IServiceCollection AddCurrentUser()
        {
            services.AddScoped<CurrentUser>();
            services.AddScoped<ICurrentUser>(static sp => sp.GetRequiredService<CurrentUser>());
            return services;
        }
    }

    extension(IServiceScopeFactory serviceScopeFactory)
    {
        /// <summary>
        /// Creates a scope whose services act as <paramref name="userId"/>, for work outside a request such as
        /// background jobs.
        /// </summary>
        /// <param name="userId">The user.</param>
        public AsyncServiceScope CreateUserScope(Guid userId)
        {
            AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<CurrentUser>().Set(userId);
            return scope;
        }
    }
}
