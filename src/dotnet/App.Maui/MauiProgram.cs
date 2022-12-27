using ActualChat.Hosting;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualChat.UI.Blazor.App;
using ActualChat.App.Maui.Services;
using ActualChat.UI.Blazor.Services;
using Microsoft.Extensions.Hosting;
using ActualChat.Audio.WebM;
using Microsoft.Maui.LifecycleEvents;
using ActualChat.Chat.UI.Blazor.Services;
using ActualChat.Notification.UI.Blazor;
using Microsoft.JSInterop;
using Serilog;
using Serilog.Events;

namespace ActualChat.App.Maui;

 #pragma warning disable VSTHRD002

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var loggerConfiguration = new LoggerConfiguration().MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext();
#if ANDROID
        loggerConfiguration = loggerConfiguration.WriteTo.AndroidLog()
            .Enrich.WithProperty(Serilog.Core.Constants.SourceContextPropertyName, AndroidConstants.LogTag);
#elif IOS
        loggerConfiguration = loggerConfiguration.WriteTo.NSLog();
#endif
        Log.Logger = loggerConfiguration.CreateLogger();
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

        try {
            Log.Information("Starting to build actual.chat maui app");
            var app = CreateMauiAppInternal();
            Log.Information("Successfully built actual.chat maui app");
            return app;
        }
        catch (Exception ex) {
            Log.Fatal(ex, "Failed to build actual.chat maui app");
            throw;
        }
    }

    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Information("Unhandled exception, isTerminating={IsTerminating}. \n{Exception}",
            e.IsTerminating,
            e.ExceptionObject);
    }

    private static MauiApp CreateMauiAppInternal()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            })
            .ConfigureLifecycleEvents(ConfigureLifecycleEvents)
            .Logging.AddDebug();

        var services = builder.Services;
        services.AddMauiBlazorWebView();

#if DEBUG || DEBUG_MAUI
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        services.AddLogging(logging => logging
            .AddDebug()
            .AddSerilog(Log.Logger, dispose: true)
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
            AppKind = AppKind.Maui,
            Environment = environment,
            Configuration = c.GetRequiredService<IConfiguration>(),
            BaseUrl = GetBaseUrl(),
            Platform = PlatformInfoProvider.GetPlatform(),
        });

        builder.ConfigureMauiHandlers(handlers => {
            handlers.AddHandler<IBlazorWebView, MauiBlazorWebViewHandler>();
        });

#if ANDROID
        services.AddSingleton<Java.Util.Concurrent.IExecutorService>(_ =>
            Java.Util.Concurrent.Executors.NewWorkStealingPool()!);
#endif
        ConfigureServices(services);

        var mauiApp = builder.Build();

        AppServices = mauiApp.Services;

        Constants.HostInfo = AppServices.GetRequiredService<HostInfo>();
        if (Constants.DebugMode.WebMReader)
            WebMReader.DebugLog = AppServices.LogFor(typeof(WebMReader));

        EnsureSessionInfoCreated(mauiApp.Services);

        // MAUI does not start HostedServices, so we do this manually.
        // https://github.com/dotnet/maui/issues/2244
        StartHostedServices(mauiApp);

        return mauiApp;
    }

    private static void EnsureSessionInfoCreated(IServiceProvider services)
        => Task.Run(async () => {
            var log = services.GetRequiredService<ILogger<MauiApp>>();
            try {
                var mobileAuthClient = services.GetRequiredService<MobileAuthClient>();
                log.LogInformation("Creating session...");
                if (!await mobileAuthClient.SetupSession().ConfigureAwait(false))
                    throw StandardError.StateTransition(nameof(MauiProgram), "Can not setup session");
                log.LogInformation("Creating session... Completed");
            }
            catch (Exception e) {
                log.LogError(e, "Failed to create session");
            }

        }).Wait();

    private static void StartHostedServices(MauiApp mauiApp)
        => mauiApp.Services.HostedServices().Start()
            .Wait(); // wait on purpose, CreateMauiApp is synchronous.

    private static string GetBaseUrl()
    {
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
                _ = HandleBackPressed(activity);
                return true;
            });
        });
#endif
    }

#if ANDROID
    private static async Task HandleBackPressed(Android.App.Activity activity)
    {
        var webView = Application.Current?.MainPage is MainPage mainPage ? mainPage.PlatformWebView : null;
        var goBack = webView != null ? await TryGoBack(webView).ConfigureAwait(false) : false;
        if (goBack)
            return;
        // Move app to background as Home button acts.
        // It prevents scenario when app is running, but activity is destroyed.
        activity.MoveTaskToBack(true);
    }

    private static async Task<bool> TryGoBack(Android.Webkit.WebView webView)
    {
        var canGoBack = webView.CanGoBack();
        if (canGoBack) {
            webView.GoBack();
            return true;
        }
        // Sometimes Chromium reports that it can't go back while there are 2 items in the history.
        // It seems that this bug exists for a while, not fixed yet and there is not plans to do it.
        // https://bugs.chromium.org/p/chromium/issues/detail?id=1098388
        // https://github.com/flutter/flutter/issues/59185
        // But we can use web api to navigate back.
        var list = webView.CopyBackForwardList();
        var canGoBack2 = list.Size > 1 && list.CurrentIndex > 0;
        if (canGoBack2) {
            if (ScopedServicesAccessor.IsInitialized) {
                var jsRuntime = ScopedServicesAccessor.ScopedServices.GetRequiredService<IJSRuntime>();
                await jsRuntime.InvokeVoidAsync("eval", "history.back()").ConfigureAwait(false);
                return true;
            }
        }
        return false;
    }

#endif

    private static void ConfigureServices(IServiceCollection services)
    {
        // HttpClient
#if !WINDOWS
        services.RemoveAll<IHttpClientFactory>();
        services.AddSingleton<NativeHttpClientFactory>(sp => new NativeHttpClientFactory(sp));
        services.TryAddSingleton<IHttpClientFactory>(serviceProvider => serviceProvider.GetRequiredService<NativeHttpClientFactory>());
        services.TryAddSingleton<IHttpMessageHandlerFactory>(serviceProvider => serviceProvider.GetRequiredService<NativeHttpClientFactory>());
#endif
        AppStartup.ConfigureServices(services, typeof(Module.BlazorUIClientAppModule)).Wait();

        // Auth
        services.AddScoped<IClientAuth>(sp => new MauiClientAuth(sp));
        services.AddTransient<MobileAuthClient>(sp => new MobileAuthClient(sp));

        // UI
        services.AddSingleton<NavigationInterceptor>(sp => new NavigationInterceptor(sp));
        services.AddTransient<MainPage>();

#if ANDROID
        services.AddTransient<Notification.UI.Blazor.IDeviceTokenRetriever>(sp => new AndroidDeviceTokenRetriever());
        services.AddScoped<IAudioOutputController>(sp => new AndroidAudioOutputController(sp));
#elif IOS
        services.AddTransient<IDeviceTokenRetriever, IOSDeviceTokenRetriever>();
#endif

        // Misc.
        services.AddScoped<DisposeTracer>(sp => new DisposeTracer());
    }

    private static Symbol GetSessionId()
        => BackgroundTask.Run(async () => {
            Symbol sessionId = Symbol.Empty;
            const string sessionIdStorageKey = "Fusion.SessionId";
            var storage = SecureStorage.Default;
            var storedSessionId = await storage.GetAsync(sessionIdStorageKey).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(storedSessionId))
                sessionId = storedSessionId;
            if (sessionId.IsEmpty) {
                sessionId = new SessionFactory().CreateSession().Id;
                await storage.SetAsync(sessionIdStorageKey, sessionId.Value).ConfigureAwait(false);
            }
            return sessionId;
        }).Result;
}
