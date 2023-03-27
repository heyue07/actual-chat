using ActualChat.Notification;
using ActualChat.Notification.UI.Blazor;
using ActualChat.UI.Blazor.Services;
using Foundation;
using Plugin.Firebase.CloudMessaging;
using Plugin.Firebase.CloudMessaging.EventArgs;
using Plugin.Firebase.iOS;
using Plugin.Firebase.Shared;
using UIKit;
using UserNotifications;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ActualChat.App.Maui;

public class PushNotifications : IDeviceTokenRetriever, IHasServices, INotificationPermissions, IDisposable
{
    private NotificationUI? _notificationUI;
    private LoadingUI? _loadingUI;

    public IServiceProvider Services { get; }
    private IFirebaseCloudMessaging Messaging { get; }
    private LoadingUI LoadingUI => _loadingUI ??= Services.GetRequiredService<LoadingUI>();
    private NotificationUI NotificationUI => _notificationUI ??= Services.GetRequiredService<NotificationUI>();
    private ILogger Log { get; }

    public PushNotifications(IServiceProvider services)
    {
        Services = services;
        Messaging = services.GetRequiredService<IFirebaseCloudMessaging>();
        Log = services.LogFor<PushNotifications>();

        Messaging.NotificationTapped += OnNotificationTapped;
    }

    public static void Initialize(UIApplication app, NSDictionary options)
    {
        // prevent null ref for windows+iphone
        // see https://github.com/xamarin/GoogleApisForiOSComponents/issues/577
#if !HOTRESTART
        var settings = new CrossFirebaseSettings(isCloudMessagingEnabled: true);
        CrossFirebase.Initialize(app, options, settings);
#endif
    }

    public void Dispose()
        => Messaging.NotificationTapped -= OnNotificationTapped;

    public Task<string?> GetDeviceToken(CancellationToken cancellationToken)
        => Messaging.GetTokenAsync();

    public async Task<PermissionState> GetNotificationPermissionState(CancellationToken cancellationToken)
    {
        var settings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync().ConfigureAwait(false);
        switch (settings.AuthorizationStatus) {
        case UNAuthorizationStatus.NotDetermined:
            return PermissionState.Prompt;
        case UNAuthorizationStatus.Denied:
            return PermissionState.Denied;
        case UNAuthorizationStatus.Authorized:
        case UNAuthorizationStatus.Provisional:
        case UNAuthorizationStatus.Ephemeral:
            return PermissionState.Granted;
        default:
            throw new ArgumentOutOfRangeException(nameof(settings.AuthorizationStatus), settings.AuthorizationStatus, null);
        }
    }

    public async Task RequestNotificationPermissions(CancellationToken cancellationToken)
    {
        await Messaging.CheckIfValidAsync().ConfigureAwait(false);
        var newState = await GetNotificationPermissionState(cancellationToken).ConfigureAwait(false);
        NotificationUI.UpdateNotificationStatus(newState);
    }

    private void OnNotificationTapped(object? sender, FCMNotificationTappedEventArgs e)
    {
        if (!e.Notification.Data.TryGetValue(NotificationConstants.MessageDataKeys.Link, out var url)) {
            Log.LogWarning("No message link received within notification");
            return;
        }
        _ = Handle();

        async Task Handle()
        {
            await LoadingUI.WhenLoaded.ConfigureAwait(false);
            Log.LogDebug("NotificationTap navigates to '{Url}'", url);
            NotificationUI.DispatchNotificationNavigation(url);
        }
    }
}
