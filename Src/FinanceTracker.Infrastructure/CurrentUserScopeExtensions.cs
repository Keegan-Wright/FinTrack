using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Infrastructure;

public static class CurrentUserScopeExtensions
{
    /// <summary>
    /// Registers <see cref="CurrentUser"/> as the scope's <see cref="ICurrentUser"/>.
    /// </summary>
    public static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(static sp => sp.GetRequiredService<CurrentUser>());
        return services;
    }

    /// <summary>
    /// Creates a scope whose services act as <paramref name="userId"/>, for work outside a request such as
    /// background jobs.
    /// </summary>
    public static AsyncServiceScope CreateUserScope(this IServiceScopeFactory scopeFactory, Guid userId)
    {
        AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().Set(userId);
        return scope;
    }
}
