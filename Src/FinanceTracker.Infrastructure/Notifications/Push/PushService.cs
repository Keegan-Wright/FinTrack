using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainPushDevice = FinanceTracker.Domain.Notifications.PushDevice;

namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <inheritdoc cref="IPushService"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IPushService>]
public sealed class PushService : ServiceBase<PushService>, IPushService
{
    private readonly IVapidKeyStore _vapidKeyStore;
    private readonly IPushSender _pushSender;
    private readonly TimeProvider _timeProvider;

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="vapidKeyStore">The server's key pair, whose public key browsers subscribe with.</param>
    /// <param name="pushSender">Sends the pushes.</param>
    /// <param name="timeProvider">The clock.</param>
    public PushService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<PushService> logger,
        IVapidKeyStore vapidKeyStore, IPushSender pushSender, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _vapidKeyStore = vapidKeyStore;
        _pushSender = pushSender;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<PushSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        return await SettingsAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PushDeviceAdded> AddDeviceAsync(AddPushDevice device, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);

        string endpoint = device.Endpoint.Trim();
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Across every user, as a browser has one subscription whoever is signed in
        DomainPushDevice? existing = await context.PushDevices
            .WhereEncryptedEquals(x => x.Endpoint, endpoint, context)
            .SingleOrDefaultAsync(cancellationToken);

        DomainPushDevice added;
        if (existing is not null && existing.UserId == UserId)
        {
            existing.Renew(device.PublicKey, device.AuthSecret, device.Name);
            added = existing;
        }
        else
        {
            if (existing is not null)
            {
                context.PushDevices.Remove(existing);
                Logger.LogPushDeviceChangedHands(existing.Id, existing.UserId, UserId);
            }

            added = new DomainPushDevice(UserId, endpoint, device.PublicKey, device.AuthSecret, device.Name);
            context.PushDevices.Add(added);
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "added", "push device", added.Id);
        return new PushDeviceAdded { DeviceId = added.Id, Settings = await SettingsAsync(context, cancellationToken) };
    }

    /// <inheritdoc/>
    public async Task<PushSettings?> RemoveDeviceAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainPushDevice? device = await context.PushDevices.OwnedBy(UserId)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (device is null)
        {
            return null;
        }

        context.PushDevices.Remove(device);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "removed", "push device", id);
        return await SettingsAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PushSettings?> SendTestAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<DomainPushDevice> devices =
            await context.PushDevices.OwnedBy(UserId).ToListAsync(cancellationToken);
        if (devices.Count == 0)
        {
            return null;
        }

        foreach (DomainPushDevice device in devices)
        {
            await SendTestToAsync(context, device, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return await SettingsAsync(context, cancellationToken);
    }

    /// <summary>
    /// Pushes the test to <paramref name="device"/> and records how it went, removing it when its browser is gone.
    /// </summary>
    /// <param name="context">The context it's tracked in.</param>
    /// <param name="device">The device.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task SendTestToAsync(FinanceTrackerContext context, DomainPushDevice device,
        CancellationToken cancellationToken)
    {
        try
        {
            PushOutcome outcome = await _pushSender.SendAsync(device, PushMessage.Test, cancellationToken);
            if (outcome == PushOutcome.Gone)
            {
                context.PushDevices.Remove(device);
                Logger.LogPushDeviceGone(UserId, device.Id);
                return;
            }

            device.RecordPush(_timeProvider.UtcNow, null);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.LogTestPushFailed(exception, UserId, device.Id);
            device.RecordPush(_timeProvider.UtcNow, PushFailure.Describe(exception));
        }
    }

    /// <summary>
    /// The user's devices and the key to subscribe with.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<PushSettings> SettingsAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        VapidKeys keys = await _vapidKeyStore.GetAsync(cancellationToken);
        List<DomainPushDevice> devices = await context.PushDevices.AsNoTracking().OwnedBy(UserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return new PushSettings { PublicKey = keys.PublicKeyText, Devices = devices.Select(ToContract).ToList() };
    }

    /// <summary>
    /// The device as the API gives it.
    /// </summary>
    /// <param name="device">The device.</param>
    private static PushDevice ToContract(DomainPushDevice device) => new()
    {
        Id = device.Id,
        Name = device.Name,
        Added = device.Created,
        LastPush = LastPush(device)
    };

    /// <summary>
    /// How the last push went, null when there hasn't been one.
    /// </summary>
    /// <param name="device">The device.</param>
    private static DeliveryStatus? LastPush(DomainPushDevice device)
    {
        if (device.LastPushAt is not { } at)
        {
            return null;
        }

        return new DeliveryStatus { At = at, Failure = device.LastPushFailure };
    }
}
