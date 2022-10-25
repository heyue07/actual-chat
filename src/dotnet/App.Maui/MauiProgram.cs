using ActualChat.Hosting;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using ActualChat.UI.Blazor.App;
using ActualChat.App.Maui.Services;
using ActualChat.UI.Blazor.Services;
using Microsoft.Extensions.Hosting;
using ActualChat.Audio.WebM;
using Microsoft.Maui.LifecycleEvents;

namespace ActualChat.App.Maui;

 #pragma warning disable VSTHRD002

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            })
            .ConfigureLifecycleEvents(ConfigureLifecycleEvents);

        var fileProvider = new EmbeddedFileProvider(typeof(MauiProgram).Assembly);
        var files = fileProvider.GetDirectoryContents("").ToArray();
        builder.Configuration.AddJsonFile(
            fileProvider,
            "appsettings.Development.json",
            optional: true,
            reloadOnChange: false);
        builder.Configuration.AddJsonFile(
            fileProvider,
            "appsettings.json",
            optional: true,
            reloadOnChange: false);

        var services = builder.Services;
        services.AddMauiBlazorWebView();

#if DEBUG || DEBUG_MAUI
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        services.AddLogging(logging => logging
            .AddDebug()
            .SetMinimumLevel(LogLevel.Information)
        );

        services.TryAddSingleton(builder.Configuration);

        var sessionId = GetSessionId();
        var settings = new ClientAppSettings { SessionId = sessionId };
        services.TryAddSingleton(settings);

#if IS_FIXED_ENVIRONMENT_PPRODUCTION
        var environment = Environments.Production;
#else
        var environment = Environments.Development;
#endif

        services.AddSingleton(c => new HostInfo {
            HostKind = HostKind.Maui,
            RequiredServiceScopes = ImmutableHashSet<Symbol>.Empty
                .Add(ServiceScope.Client)
                .Add(ServiceScope.BlazorUI),
            Environment = environment,
            Configuration = c.GetRequiredService<IConfiguration>(),
            BaseUrl = GetBaseUrl(),
        });

        builder.ConfigureMauiHandlers(handlers => {
            handlers.AddHandler<IBlazorWebView, MauiBlazorWebViewHandler>();
        });

        ConfigureServices(services);

        var mauiApp = builder.Build();

        AppServices = mauiApp.Services;

        Constants.HostInfo = AppServices.GetRequiredService<HostInfo>();
        if (Constants.DebugMode.WebMReader)
            WebMReader.DebugLog = AppServices.LogFor(typeof(WebMReader));

        // MAUI does not start HostedServices, so we do this manually.
        // https://github.com/dotnet/maui/issues/2244
        StartHostedServices(mauiApp);

        return mauiApp;
    }

    private static void StartHostedServices(MauiApp mauiApp)
        => mauiApp.Services.HostedServices().Start()
            .Wait(); // wait on purpose, CreateMauiApp is synchronous.

    private static string GetBaseUrl()
    {
        // Host address for local debugging
        // https://devblogs.microsoft.com/xamarin/debug-local-asp-net-core-web-apis-android-emulators/
        // https://developer.android.com/studio/run/emulator-networking.html
        // Unfortunately, this base address does not work in WSA.
        // TODO(DF): find solution for WSA
        var ipAddress = DeviceInfo.Platform == DevicePlatform.Android ? "10.0.2.2" : "localhost";
        var baseUrl = $"http://{ipAddress}:7080/";

        // To use BaseUri : https://local.actual.chat
        // We need to modify hosts file on Android emulator similarly to how we did it for Windows hosts.
        // Using instructions from https://csimpi.medium.com/android-emulator-add-hosts-file-f4c73447453e,
        // add line to the hosts file:
        // 10.0.2.2		local.actual.chat
        // Emulator has to be started with -writable-system flag every time to see hosts changes
        // See comments to https://stackoverflow.com/questions/41117715/how-to-edit-etc-hosts-file-in-android-studio-emulator-running-in-nougat/47622017#47622017

        //return "https://local.actual.chat";
#if ISDEVMAUI
        return "https://dev.actual.chat/";
#else
        return "https://actual.chat/";
#endif
    }

    private static void ConfigureLifecycleEvents(ILifecycleBuilder events)
    {
#if ANDROID
        events.AddAndroid(android => {
            android.OnBackPressed(activity => {
                // Sometimes I observe a situation that BlazorWebView contains 2 history items and can go back,
                // but it doesn't handle BackPressed event.
                // This handler forces BackPressed handling on BlazorWebView and
                // prevents the situation that app is closed on BackPressed
                // while BlazorWebView still can go back.
                if (Application.Current?.MainPage is MainPage mainPage) {
                    var webView = mainPage.PlatformWebView;
                    if (webView != null && webView.CanGoBack()) {
                        webView.GoBack();
                        return true;
                    }
                }
                // Move app to background as Home button acts.
                // It prevents scenario when app is running, but activity is destroyed.
                activity.MoveTaskToBack(true);
                return true;
            });
        });
#endif
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        AppConfigurator.ConfigureServices(services, typeof(Module.BlazorUIClientAppModule)).Wait();

        // Auth
        services.AddScoped<IClientAuth, MauiClientAuth>();
        services.AddTransient<MobileAuthClient>();

        // UI
        services.AddSingleton<NavigationInterceptor>();
        services.AddTransient<MainPage>();

        //Firebase messaging
        #if ANDROID
        services.AddTransient<Notification.UI.Blazor.IDeviceTokenRetriever, AndroidDeviceTokenRetriever>();
        #endif
    }

    private static Symbol GetSessionId()
    {
        const string sessionIdStorageKey = "Fusion.SessionId";
        Symbol sessionId = Symbol.Empty;
        if (Preferences.ContainsKey(sessionIdStorageKey)) {
            var value = Preferences.Get(sessionIdStorageKey, null);
            if (!string.IsNullOrEmpty(value))
                sessionId = value;
        }
        if (sessionId.IsEmpty) {
            sessionId = new SessionFactory().CreateSession().Id;
            Preferences.Set(sessionIdStorageKey, sessionId.Value);
        }
        return sessionId;
    }
}
