using FinanceTracker.Ai.Persistence;
using FinanceTracker.Infrastructure.Common.Tokens;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Ai.Mcp;

/// <summary>
/// Lets an AI client's token sign in only while its user has AI turned on, so turning it off on Your profile cuts
/// off their AI clients too.
/// </summary>
/// <param name="contextFactory">Reads whether the user has turned AI on.</param>
public sealed class AiTokenScopeCheck(IDbContextFactory<FinanceTrackerAiContext> contextFactory) : ITokenScopeCheck
{
    /// <summary>
    /// The core's name for the AI clients' scope.
    /// </summary>
    public string Scope => "Ai";

    /// <inheritdoc/>
    public async Task<bool> AllowsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerAiContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.UserSettings.AnyAsync(x => x.UserId == userId && x.OptedIn, cancellationToken);
    }
}
