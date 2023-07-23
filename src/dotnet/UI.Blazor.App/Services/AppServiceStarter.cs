using ActualChat.Chat;
using ActualChat.Chat.UI.Blazor.Services;
using ActualChat.Contacts;
using ActualChat.Hosting;
using ActualChat.UI.Blazor.Services;
using ActualChat.Users;

namespace ActualChat.UI.Blazor.App.Services;

public class AppServiceStarter
{
    private HostInfo? _hostInfo;
    private History? _history;
    private volatile string? _secondaryAutoNavigationUrl;

    private IServiceProvider Services { get; }
    private Tracer Tracer { get; }
    private HostInfo HostInfo => _hostInfo ??= Services.GetRequiredService<HostInfo>();
    private History History => _history ??= Services.GetRequiredService<History>();

    public AppServiceStarter(IServiceProvider services)
    {
        Services = services;
        Tracer = Services.Tracer(GetType());
    }

    public Task PostSessionWarmup(CancellationToken cancellationToken)
        => Task.Run(async () => {
            using var _1 = Tracer.Region();
            try {
                // NOTE(AY): This code runs in the root scope, so you CAN'T access any scoped services here!
                var session = Session.Default; // All clients use default session

                // Access key services
                var accounts = Services.GetRequiredService<IAccounts>();
                var contacts = Services.GetRequiredService<IContacts>();
                Services.GetRequiredService<IChats>();

                // Preload own account
                var ownAccountTask = accounts.GetOwn(session, cancellationToken);

                // Start preloading top contacts
                var contactIdsTask = contacts.ListIds(session, cancellationToken);
                var contactIds = await contactIdsTask.ConfigureAwait(false);
                foreach (var contactId in contactIds.Take(Constants.Contacts.MinLoadLimit))
                    _ = contacts.Get(session, contactId, cancellationToken);

                // Complete own account preloading
                await ownAccountTask.ConfigureAwait(false);

                // _ = Task.Run(WarmupSystemJsonSerializer, CancellationToken.None);
            }
            catch (Exception e) {
                Tracer.Point($"{nameof(PostSessionWarmup)} failed, error: " + e);
            }
        }, cancellationToken);

    public async Task ReadyToRender(string sessionHash, CancellationToken cancellationToken)
    {
        using var _1 = Tracer.Region();

        // Starting initial navigation URL resolving.
        // This requires AccountUI.OwnAccount & ChatUI.SelectedChatId,
        // so both of these services are started even earlier
        var autoNavigationUI = Services.GetRequiredService<AutoNavigationUI>();
        var history = History;
        // Note that GetAutoNavigationUrl must start in Blazor Dispatcher
        var autoNavigationUrlTask = autoNavigationUI.GetAutoNavigationUrl(cancellationToken);

        // Creating core services - this should be done as early as possible
        var browserInfo = Services.GetRequiredService<BrowserInfo>();

        // Initializing them at once
        Tracer.Point("BulkInitUI.Invoke");
        var browserInit = Services.GetRequiredService<BrowserInit>();
        var baseUri = HostInfo.BaseUrl;
        var browserInitTask = browserInit.Initialize(
            Constants.Api.Version, baseUri, sessionHash,
            async initCalls => {
                await browserInfo.Initialize(initCalls).ConfigureAwait(false);
            });

        // Starting ThemeUI
        Tracer.Point("ThemeUI.Start");
        var themeUI = Services.GetRequiredService<ThemeUI>();
        _ = Task.Run(() => themeUI.Start(), CancellationToken.None);

        // Awaiting for completion of initialization tasks.
        // NOTE(AY): it's fine to use .ConfigureAwait(false) below this point,
        //           coz tasks were started on Dispatcher thread already.

        // Finishing w/ BrowserInfo
        await browserInfo.WhenReady.ConfigureAwait(false);
        Tracer.Point("BrowserInfo is ready");

        var timeZoneConverter = Services.GetRequiredService<TimeZoneConverter>();
        if (timeZoneConverter is ServerSideTimeZoneConverter serverSideTimeZoneConverter)
            serverSideTimeZoneConverter.Initialize(browserInfo.UtcOffset);

        // Finishing w/ ThemeUI
        await themeUI.WhenReady.ConfigureAwait(false);
        Tracer.Point("ThemeUI is ready");

        // Finishing with BrowserInit
        await browserInitTask.ConfigureAwait(false); // Must be completed before the next call

        // Finishing with auto-navigation & History init
        var autoNavigationUrl = await autoNavigationUrlTask.ConfigureAwait(false);
        if (autoNavigationUrl.IsChat() && browserInfo.ScreenSize.Value.IsNarrow()) {
            // We have to open chat root first - to make sure "Back" leads to it
            Interlocked.Exchange(ref _secondaryAutoNavigationUrl, autoNavigationUrl.Value);
            autoNavigationUrl = Links.Chats;
        }
        await history.Initialize(autoNavigationUrl).ConfigureAwait(false);
    }

    public async Task AfterRender(CancellationToken cancellationToken)
    {
        if (_secondaryAutoNavigationUrl is { } secondaryAutoNavigationUrl)
            _ = History.Dispatcher.InvokeAsync(() => History.NavigateTo(secondaryAutoNavigationUrl));

        // Starting less important UI services
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        if (HostInfo.AppKind.IsClient())
            Services.GetRequiredService<SessionTokens>().Start();
        Services.GetRequiredService<AppPresenceReporter>().Start();
        Services.GetRequiredService<AppIconBadgeUpdater>().Start();
        Services.GetService<RpcPeerStateMonitor>()?.Start(); // Available only on the client
        if (HostInfo.AppKind.IsClient()) {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            await StartHostedServices(cancellationToken).ConfigureAwait(false);
        }
        if (!HostInfo.IsProductionInstance)
            Services.GetRequiredService<DebugUI>();
    }

    // Private methods

    private async Task StartHostedServices(CancellationToken cancellationToken)
    {
        using var _ = Tracer.Region();
        foreach (var hostedService in Services.HostedServices()) {
            Tracer.Point($"{nameof(StartHostedServices)}: starting {hostedService.GetType().Name}");
            await hostedService.StartAsync(default).ConfigureAwait(false);
        }
    }

    private void WarmupSystemJsonSerializer()
    {
        Read<ThemeSettings>("{\"theme\":1,\"origin\":\"\"}");
        Read<UserLanguageSettings>("{\"primary\":\"fr-FR\",\"secondary\":null,\"origin\":\"\"}");
        Read<UserOnboardingSettings>("{\"isPhoneStepCompleted\":false,\"isAvatarStepCompleted\":true,\"lastShownAt\":\"1970-01-01T00:00:00.0000000Z\",\"origin\":\"\"}");
        Read<UserBubbleSettings>("{\"readBubbles\":[\"x\"],\"origin\":\"\"}");
        Read<ChatListSettings>("{\"order\":3,\"filterId\":\"\"}");
        Read<ApiArray<ActiveChat>>("[{\"chatId\":\"dpwo1tm0tw\",\"isListening\":false,\"isRecording\":false,\"recency\":\"1970-01-01T00:00:00.0000000Z\",\"listeningRecency\":\"1970-01-01T00:00:00.0000000Z\"}]");

        static void Read<T>(string json)
        {
            try {
                SystemJsonSerializer.Default.Read<T>(json);
            }
            catch {
                // Intended
            }
        }
    }
}
