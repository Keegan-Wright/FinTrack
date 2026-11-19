using FinanceTracker.Components.Account;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Tokens;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tokens;

/// <summary>
/// <see cref="ITokensService"/> for the signed in user. Only the hash of a token's secret is kept, so the token is
/// shown the once it's made.
/// </summary>
public sealed class TokensService : ITokensService
{
    private readonly IDbContextFactory<FinanceTrackerContext> _contextFactory;
    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly ICurrentUser _currentUser;
    private readonly SignInMethods _signInMethods;
    private readonly ISecurityEmails _securityEmails;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TokensService> _logger;

    /// <param name="contextFactory">Keeps the tokens.</param>
    /// <param name="userManager">Checks the user's password.</param>
    /// <param name="currentUser">The signed in user.</param>
    /// <param name="signInMethods">Whether passwords can be used.</param>
    /// <param name="securityEmails">Tells the user when their tokens change.</param>
    /// <param name="timeProvider">The clock expiry is worked out from.</param>
    /// <param name="logger">Its logger.</param>
    public TokensService(IDbContextFactory<FinanceTrackerContext> contextFactory,
        UserManager<FinanceTrackerUser> userManager, ICurrentUser currentUser, SignInMethods signInMethods,
        ISecurityEmails securityEmails, TimeProvider timeProvider, ILogger<TokensService> logger)
    {
        _contextFactory = contextFactory;
        _userManager = userManager;
        _currentUser = currentUser;
        _signInMethods = signInMethods;
        _securityEmails = securityEmails;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TokenSummary>?> GetTokensAsync(CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await UserAsync();
        if (!await CanHaveTokensAsync(user))
        {
            return null;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        await using FinanceTrackerContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        List<PersonalAccessToken> tokens = await context.PersonalAccessTokens.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.RevokedAt == null)
            .OrderByDescending(static x => x.Created)
            .ToListAsync(cancellationToken);

        return tokens.Select(x => new TokenSummary
            {
                Id = x.Id,
                Name = x.Name,
                CreatedAt = x.Created,
                ExpiresAt = x.ExpiresAt,
                LastUsedAt = x.LastUsedAt,
                Expired = !x.IsActive(now)
            })
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<CreatedToken?> CreateAsync(CreateToken request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinanceTrackerUser user = await UserAsync();
        if (!await CanHaveTokensAsync(user))
        {
            return null;
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return new CreatedToken { Errors = ["Your password isn't right"] };
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        IssuedToken issued = PersonalAccessTokenFormat.Issue();
        PersonalAccessToken token = new(user.Id, request.Name, issued.LookupId, issued.SecretHash,
            ExpiresAt(request.Expiry, now));

        await using (FinanceTrackerContext context = await _contextFactory.CreateDbContextAsync(cancellationToken))
        {
            context.PersonalAccessTokens.Add(token);
            await context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogTokenCreated(user.Id, token.Id);
        await _securityEmails.TokenCreatedAsync(user, token.Name);
        return new CreatedToken { Token = issued.Token };
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await UserAsync();
        await using FinanceTrackerContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        PersonalAccessToken? token = await context.PersonalAccessTokens
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == user.Id && x.RevokedAt == null, cancellationToken);
        if (token is null)
        {
            return false;
        }

        token.Revoke(_timeProvider.GetUtcNow().UtcDateTime);
        await context.SaveChangesAsync(cancellationToken);
        _logger.LogTokenRevoked(user.Id, token.Id);
        await _securityEmails.TokenRevokedAsync(user, token.Name);
        return true;
    }

    /// <summary>
    /// When a token made at <paramref name="now"/> lasting <paramref name="expiry"/> stops working, null for never.
    /// </summary>
    /// <param name="expiry">How long it lasts.</param>
    /// <param name="now">The time now, in UTC.</param>
    private static DateTime? ExpiresAt(TokenExpiry expiry, DateTime now)
    {
        switch (expiry)
        {
            case TokenExpiry.ThirtyDays:
                return now.AddDays(30);
            case TokenExpiry.NinetyDays:
                return now.AddDays(90);
            case TokenExpiry.OneYear:
                return now.AddYears(1);
            case TokenExpiry.Never:
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(expiry), expiry, null);
        }
    }

    /// <summary>
    /// The signed in user.
    /// </summary>
    /// <exception cref="InvalidOperationException">They no longer exist.</exception>
    private async Task<FinanceTrackerUser> UserAsync() =>
        await _userManager.FindByIdAsync(_currentUser.UserId.ToString())
        ?? throw new InvalidOperationException("The signed in user no longer exists");

    /// <summary>
    /// Whether <paramref name="user"/> can have tokens. Only with a password they can use, see
    /// <see cref="SignInMethods.CanUsePassword"/>, as making one asks for it.
    /// </summary>
    /// <param name="user">The user.</param>
    private async Task<bool> CanHaveTokensAsync(FinanceTrackerUser user) =>
        _signInMethods.CanUsePassword(await _userManager.HasPasswordAsync(user));
}
