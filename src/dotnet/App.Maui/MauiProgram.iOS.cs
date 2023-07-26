using ActualChat.Notification.UI.Blazor;
using Microsoft.Maui.LifecycleEvents;
using Plugin.Firebase.CloudMessaging;
using Serilog;

namespace ActualChat.App.Maui;

public static partial class MauiProgram
{
    private static partial void AddPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton(CrossFirebaseCloudMessaging.Current);
        services.AddScoped<PushNotifications>(c => new PushNotifications(c));
        services.AddTransient<IDeviceTokenRetriever>(c => c.GetRequiredService<PushNotifications>());
        services.AddScoped<INotificationPermissions>(c => c.GetRequiredService<PushNotifications>());
        services.AddScoped(c => new NativeAppleAuth(c));
    }

    private static partial void AddPlatformServicesToSkip(HashSet<Type> servicesToSkip)
    { }

    private static partial void ConfigurePlatformLifecycleEvents(ILifecycleBuilder events)
        => events.AddiOS(ios => ios.FinishedLaunching((app, options) => {
            PushNotifications.Initialize(app, options);
            return false;
        }));
}
