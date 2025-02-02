using ActualChat.App.Maui.Services;
using ActualChat.Audio.WebM;
using ActualChat.Hosting;
using ActualChat.UI.Blazor;
using ActualChat.UI.Blazor.App;
using ActualChat.UI.Blazor.Diagnostics;
using ActualChat.UI.Blazor.Services;
using OpenTelemetry.Trace;
using Sentry;
using Sentry.Maui.Internal;
using Sentry.Serilog;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Stl.IO;
using ILogger = Serilog.ILogger;
using Tracer = ActualChat.Performance.Tracer;

namespace ActualChat.App.Maui;

public static class MauiDiagnostics
{
    private const string LogFolder = "Logs";
    private const string LogFile = "ActualChat.log";
    private const string AndroidOutputTemplate = "({ThreadID}) [{SourceContext}] {Message:l}{NewLine:l}{Exception}";
    private static readonly TimeSpan SentryStartDelay = TimeSpan.FromSeconds(5);

#if DEBUG
    public const string LogTag = "dev.actual.chat";
#else
    public const string LogTag = "actual.chat";
#endif

    private static SentryOptions? _sentryOptions;

    public static readonly ILoggerFactory LoggerFactory;
    public static readonly Tracer Tracer;
    public static TracerProvider? TracerProvider { get; private set; }

    static MauiDiagnostics()
    {
        Log.Logger = CreateAppLogger();
        Tracer.Default = Tracer = CreateAppTracer();
        LoggerFactory = new SerilogLoggerFactory(Log.Logger);
        DefaultLog = LoggerFactory.CreateLogger("ActualChat.Unknown");

        if (Constants.DebugMode.WebMReader)
            WebMReader.DebugLog = LoggerFactory.CreateLogger(typeof(WebMReader));

        if (_sentryOptions != null)
            _ = LoadingUI.WhenAppRendered
                .WithDelay(SentryStartDelay)
                .ContinueWith(_ => {
                        InitSentrySdk(_sentryOptions);
                        var _1 = CreateClientSentryTraceProvider();
                    },
                    TaskScheduler.Default);
    }

    public static IServiceCollection AddMauiDiagnostics(this IServiceCollection services, bool dispose)
    {
        services.AddSingleton<Disposer>();
        services.AddTracer(Tracer); // We don't want to have scoped tracers in MAUI app
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.ConfigureClientFilters(MauiSettings.ClientKind);
            logging.AddFilteringSerilog(Log.Logger, dispose: dispose);
        });
        return services;
    }

    // Private methods

    private static ILogger CreateAppLogger()
    {
        var logging = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.With(new ThreadIdEnricher())
            .Enrich.FromLogContext()
            .Enrich.WithProperty(Serilog.Core.Constants.SourceContextPropertyName, "app.maui");
        logging = AddPlatformLoggerSinks(logging);
        if (Constants.Sentry.EnabledFor.Contains(AppKind.MauiApp))
            logging = logging.WriteTo.Sentry(ConfigureSentrySerilog);
        return logging.CreateLogger();
    }

    private static Tracer CreateAppTracer()
    {
#if DEBUG
        var logger = Log.Logger.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "@trace");
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        return new Tracer("MauiApp", x => logger.Information(x.Format()));
#else
        return Tracer.None;
#endif
    }

    private static LoggerConfiguration AddPlatformLoggerSinks(LoggerConfiguration logging)
    {
#if WINDOWS
        var logPath = (FilePath)FileSystem.AppDataDirectory & LogFolder & LogFile;
        logging = logging.WriteTo.Debug(outputTemplate: AppLogging.DebugOutputTemplate);
        logging = logging.WriteTo.File(logPath,
            outputTemplate: AppLogging.OutputTemplate,
            fileSizeLimitBytes: AppLogging.FileSizeLimit);
#elif ANDROID
        logging = logging.WriteTo.AndroidTaggedLog(LogTag, outputTemplate: AndroidOutputTemplate);
#elif IOS
        logging = logging.WriteTo.NSLog();
#endif
        return logging;
    }

    private static void ConfigureSentrySerilog(SentrySerilogOptions options)
    {
        options.ConfigureForApp(true);
        // We'll initialize the SDK later after app is loaded.
        options.InitializeSdk = false;

        // Set defaults for options that are different for MAUI.
        options.AutoSessionTracking = true;
        options.DetectStartupTime = StartupTimeDetectionMode.Fast;
        options.CacheDirectoryPath = FileSystem.CacheDirectory;

        // Global Mode makes sense for client apps
        options.IsGlobalModeEnabled = true;

        options.MinimumEventLevel = LogEventLevel.Warning;

        // We'll use an event processor to set things like SDK name
        options.AddEventProcessor(new SentryMauiEventProcessor2(options));
        _sentryOptions = options;
    }

    private static void InitSentrySdk(SentryOptions options)
    {
        // We can use MAUI's network connectivity information to inform the CachingTransport when we're offline.
        // We do it here to eliminate startup delay due to checking connectivity status in MauiNetworkStatusListener .ctor.
        options.NetworkStatusListener = new MauiNetworkStatusListener(Connectivity.Current, options);

        // Initialize the Sentry SDK.
        var disposable = SentrySdk.Init(options);
        // TODO(DF): It seems disposer is not invoked on application closing. Look up for another solution to flush client data.
        var disposer = AppServices.GetRequiredService<Disposer>();
        // Register the return value from initializing the SDK with the disposer.
        // This will ensure that it gets disposed when the service provider is disposed.
        disposer.Register(disposable);
    }

    private static async Task CreateClientSentryTraceProvider()
    {
        // Initialize client trace provider only in development environment or for admin users.
        var urlMapper = AppServices.GetRequiredService<UrlMapper>();
        if (urlMapper.IsActualChat) {
            var scopedServices = await ScopedServicesTask.ConfigureAwait(false);
            var accountUI = scopedServices.GetRequiredService<AccountUI>();
            await accountUI.WhenLoaded.ConfigureAwait(false);
            var ownAccount = await accountUI.OwnAccount.Use().ConfigureAwait(false);
            if (!ownAccount.IsAdmin)
                return;
        }
        TracerProvider = OtelDiagnostics.CreateClientSentryTraceProvider("MauiApp");
    }

    public static ILoggingBuilder AddFilteringSerilog(
        this ILoggingBuilder builder,
        ILogger? logger = null,
        bool dispose = false)
    {
        // NOTE(AY): It's almost the same code as in .AddSerilog, but with a single line commented out (see below)
        if (builder == null)
            throw new ArgumentNullException(nameof (builder));

        if (dispose)
            builder.Services.AddSingleton<ILoggerProvider, SerilogLoggerProvider>(_ => new SerilogLoggerProvider(logger, true));
        else
            builder.AddProvider(new SerilogLoggerProvider(logger));
        // builder.AddFilter<SerilogLoggerProvider>(null, LogLevel.Trace);
        return builder;
    }
}
