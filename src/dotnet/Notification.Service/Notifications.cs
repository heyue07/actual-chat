﻿using ActualChat.Notification.Backend;
using ActualChat.Notification.Db;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Stl.Fusion.EntityFramework;

namespace ActualChat.Notification;

public partial class Notifications : DbServiceBase<NotificationDbContext>, INotifications, INotificationsBackend
{
    private readonly IAuth _auth;
    private readonly MomentClockSet _clocks;
    private readonly FirebaseMessaging _firebaseMessaging;
    private readonly IDbContextFactory<NotificationDbContext> _dbContextFactory;
    private readonly UriMapper _uriMapper;
    private readonly ILogger<Notifications> _log;

    public Notifications(
        IServiceProvider services,
        IAuth auth,
        MomentClockSet clocks,
        FirebaseMessaging firebaseMessaging,
        IDbContextFactory<NotificationDbContext> dbContextFactory,
        UriMapper uriMapper,
        ILogger<Notifications> log) : base(services)
    {
        _auth = auth;
        _clocks = clocks;
        _firebaseMessaging = firebaseMessaging;
        _dbContextFactory = dbContextFactory;
        _uriMapper = uriMapper;
        _log = log;
    }

    // [ComputeMethod]
    public virtual async Task<ChatNotificationStatus> GetStatus(Session session, string chatId, CancellationToken cancellationToken)
    {
        var user = await _auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        if (!user.IsAuthenticated)
            return ChatNotificationStatus.NotSubscribed;

        var dbContext = CreateDbContext();
        await using var _ = dbContext.ConfigureAwait(false);

        string userId = user.Id;
        var isSubscribed = await dbContext.ChatSubscriptions
            .AnyAsync(d => d.UserId == userId && d.ChatId == chatId, cancellationToken)
            .ConfigureAwait(false);
        return isSubscribed ? ChatNotificationStatus.Subscribed : ChatNotificationStatus.NotSubscribed;
    }

    // [CommandHandler]
    public virtual async Task RegisterDevice(INotifications.RegisterDeviceCommand command, CancellationToken cancellationToken)
    {
        var context = CommandContext.GetCurrent();
        if (Computed.IsInvalidating()) {
            var device = context.Operation().Items.Get<DbDevice>();
            var isNew = context.Operation().Items.GetOrDefault(false);
            if (isNew && device != null)
                _ = GetDevices(device.UserId, default);
            return;
        }

        var (session, deviceId, deviceType) = command;
        var user = await _auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        if (!user.IsAuthenticated)
            return;

        var dbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var __ = dbContext.ConfigureAwait(false);
        var existingDbDevice = await dbContext.Devices.ForUpdate()
            .SingleOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);

        var dbDevice = existingDbDevice;
        if (dbDevice == null) {
            dbDevice = new DbDevice {
                Id = deviceId,
                Type = deviceType,
                UserId = user.Id,
                Version = VersionGenerator.NextVersion(),
                CreatedAt = _clocks.SystemClock.Now,
            };
            dbContext.Add(dbDevice);
        }
        else
            dbDevice.AccessedAt = _clocks.SystemClock.Now;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        context.Operation().Items.Set(dbDevice);
        context.Operation().Items.Set(existingDbDevice == null);
    }

    // [CommandHandler]
    public virtual async Task SetStatus(INotifications.SetStatusCommand command, CancellationToken cancellationToken)
    {
        var context = CommandContext.GetCurrent();
        var (session, chatId, mustSubscribe) = command;
        if (Computed.IsInvalidating()) {
            var invWasSubscribed = context.Operation().Items.GetOrDefault(false);
            if (invWasSubscribed != mustSubscribe) {
                _ = GetStatus(session, chatId, default);
                _ = GetSubscribers(chatId, default);
            }
            return;
        }

        var user = await _auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        if (!user.IsAuthenticated)
            return;

        string userId = user.Id;
        var dbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var __ = dbContext.ConfigureAwait(false);

        var dbSubscription = await dbContext.ChatSubscriptions
            .FirstOrDefaultAsync(cs => cs.UserId == userId && cs.ChatId == chatId, cancellationToken)
            .ConfigureAwait(false);

        var isSubscribed = dbSubscription != null;
        context.Operation().Items.Set(isSubscribed);
        if (isSubscribed == mustSubscribe)
            return;

        if (mustSubscribe) {
            dbSubscription = new DbChatSubscription {
                Id = Ulid.NewUlid().ToString(),
                UserId = userId,
                ChatId = chatId,
                Version = VersionGenerator.NextVersion(),
            };
            dbContext.Add(dbSubscription);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else {
            dbContext.Remove(dbSubscription!);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
