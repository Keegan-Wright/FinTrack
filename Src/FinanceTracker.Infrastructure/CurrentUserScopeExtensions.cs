using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Infrastructure;

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

    extension(IServiceScopeFactory scopeFactory)
    {
        /// <summary>
        /// Creates a scope whose services act as <paramref name="userId"/>, for work outside a request such as
        /// background jobs.
        /// </summary>
        public AsyncServiceScope CreateUserScope(Guid userId)
        {
            AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<CurrentUser>().Set(userId);
            return scope;
        }
    }
}
