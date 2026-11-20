using System.Buffers.Text;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// <see cref="IVapidKeyStore"/> over the database's one <see cref="StoredVapidKeys"/> row, kept in memory once read.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Singleton<IVapidKeyStore>]
public sealed class VapidKeyStore : IVapidKeyStore, IDisposable
{
    private readonly IDbContextFactory<FinanceTrackerContext> _financeTrackerContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _loading = new(1, 1);
    private VapidKeys? _keys;

    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="timeProvider">The clock.</param>
    public VapidKeyStore(IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        TimeProvider timeProvider)
    {
        _financeTrackerContextFactory = financeTrackerContextFactory;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<VapidKeys> GetAsync(CancellationToken cancellationToken)
    {
        if (_keys is not null)
        {
            return _keys;
        }

        await _loading.WaitAsync(cancellationToken);
        try
        {
            _keys ??= await LoadOrCreateAsync(cancellationToken);
            return _keys;
        }
        finally
        {
            _loading.Release();
        }
    }

    /// <summary>
    /// The saved pair, or a new one saved now. When another server saves one first, that one is used.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<VapidKeys> LoadOrCreateAsync(CancellationToken cancellationToken)
    {
        if (await FindAsync(cancellationToken) is { } saved)
        {
            return saved;
        }

        VapidKeys created = VapidKeys.Create();
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        context.VapidKeys.Add(new StoredVapidKeys
        {
            PublicKey = Base64Url.EncodeToString(created.PublicKey),
            PrivateKey = Base64Url.EncodeToString(created.PrivateKey),
            Created = _timeProvider.UtcNow
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            // Another server saved its pair first
            return await FindAsync(cancellationToken)
                   ?? throw new InvalidOperationException("The VAPID keys couldn't be saved or read.");
        }
    }

    /// <summary>
    /// The saved pair, null when there isn't one yet.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<VapidKeys?> FindAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        StoredVapidKeys? stored = await context.VapidKeys.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == StoredVapidKeys.OnlyId, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        return new VapidKeys(Base64Url.DecodeFromChars(stored.PublicKey), Base64Url.DecodeFromChars(stored.PrivateKey));
    }

    /// <inheritdoc/>
    public void Dispose() => _loading.Dispose();
}
