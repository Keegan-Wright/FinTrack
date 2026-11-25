using FinanceTracker.Ai.Contracts;
using FinanceTracker.Ai.Persistence;
using FinanceTracker.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Ai.Services;

/// <inheritdoc cref="IAiService"/>
/// <param name="currentUser">The signed in user.</param>
/// <param name="contextFactory">Makes the contexts it reads and saves through.</param>
/// <param name="configuration">The app's settings, for <see cref="OllamaEndpointVariable"/>.</param>
public sealed class AiService(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerAiContext> contextFactory,
    IConfiguration configuration) : IAiService
{
    /// <summary>
    /// The environment variable giving the install's Ollama endpoint, until an admin saves one.
    /// </summary>
    public const string OllamaEndpointVariable = "OLLAMA_ENDPOINT";

    /// <inheritdoc/>
    public async Task<AiStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerAiContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await StatusAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AiStatus> ChangeOptInAsync(ChangeAiOptIn change, CancellationToken cancellationToken)
    {
        await using FinanceTrackerAiContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        AiUserSetting? setting = await context.UserSettings
            .SingleOrDefaultAsync(x => x.UserId == currentUser.UserId, cancellationToken);
        if (setting is null)
        {
            setting = new AiUserSetting { UserId = currentUser.UserId };
            context.UserSettings.Add(setting);
        }

        setting.OptedIn = change.OptedIn;
        await context.SaveChangesAsync(cancellationToken);
        return await StatusAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AiSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerAiContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await SettingsAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AiSettings> SaveSettingsAsync(SaveAiSettings settings, CancellationToken cancellationToken)
    {
        await using FinanceTrackerAiContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        AiSetting endpoint = await context.Settings
            .SingleAsync(x => x.Name == AiSetting.OllamaEndpoint, cancellationToken);
        endpoint.Value = string.IsNullOrWhiteSpace(settings.OllamaEndpoint) ? null : settings.OllamaEndpoint.Trim();
        await context.SaveChangesAsync(cancellationToken);
        return await SettingsAsync(context, cancellationToken);
    }

    /// <summary>
    /// The user's status, read through <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<AiStatus> StatusAsync(FinanceTrackerAiContext context, CancellationToken cancellationToken)
    {
        AiSettings settings = await SettingsAsync(context, cancellationToken);
        bool optedIn = await context.UserSettings
            .AnyAsync(x => x.UserId == currentUser.UserId && x.OptedIn, cancellationToken);
        bool available = !string.IsNullOrWhiteSpace(settings.OllamaEndpoint ?? settings.EnvironmentOllamaEndpoint);
        return new AiStatus { Available = available, OptedIn = optedIn };
    }

    /// <summary>
    /// The install's settings, read through <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<AiSettings> SettingsAsync(FinanceTrackerAiContext context, CancellationToken cancellationToken)
    {
        AiSetting saved = await context.Settings.AsNoTracking()
            .SingleAsync(x => x.Name == AiSetting.OllamaEndpoint, cancellationToken);
        string? environment = configuration[OllamaEndpointVariable];
        return new AiSettings
        {
            OllamaEndpoint = saved.Value,
            EnvironmentOllamaEndpoint = string.IsNullOrWhiteSpace(environment) ? null : environment
        };
    }
}
