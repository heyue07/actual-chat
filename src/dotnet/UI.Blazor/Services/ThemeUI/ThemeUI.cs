using ActualChat.Hosting;
using ActualChat.Kvas;
using ActualChat.UI.Blazor.Module;

namespace ActualChat.UI.Blazor.Services;

public class ThemeUI : WorkerBase
{
    private readonly ISyncedState<ThemeSettings> _settings;
    private readonly TaskCompletionSource<Unit> _whenReadySource = TaskCompletionSourceExt.New<Unit>();
    // Nearly every service here is requested only when the theme is applied,
    // so it makes sense to postpone their resolution
    private HostInfo? _hostInfo;
    private Dispatcher? _dispatcher;
    private IJSRuntime? _js;
    private ILogger? _log;

    private Theme _appliedTheme = Theme.Light;

    private IServiceProvider Services { get; }
    private HostInfo HostInfo => _hostInfo ??= Services.GetRequiredService<HostInfo>();
    private Dispatcher Dispatcher => _dispatcher ??= Services.GetRequiredService<Dispatcher>();
    private IJSRuntime JS => _js ??= Services.GetRequiredService<IJSRuntime>();
    private ILogger Log => _log ??= Services.LogFor(GetType());

    public IState<ThemeSettings> Settings => _settings;
    public Theme Theme {
        get => _settings.Value.Theme;
        set => _settings.Value = new ThemeSettings(value);
    }
    public Task WhenReady => _whenReadySource.Task;

    public ThemeUI(IServiceProvider services)
    {
        Services = services;

        var stateFactory = services.StateFactory();
        var accountSettings = services.AccountSettings().WithPrefix(nameof(ThemeUI));
        _settings = stateFactory.NewKvasSynced<ThemeSettings>(
            new(accountSettings, nameof(ThemeSettings)) {
                InitialValue = new ThemeSettings(Theme.Light),
                UpdateDelayer = FixedDelayer.Instant,
                Category = StateCategories.Get(GetType(), nameof(Settings)),
            });

        // We don't have themes yet, so it's safe to indicate the theme is ready immediately
        _whenReadySource.TrySetResult(default);
    }

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        await _settings.WhenFirstTimeRead.ConfigureAwait(false);

        await foreach (var cTheme in Settings.Changes(cancellationToken).ConfigureAwait(false))
            await ApplyTheme(cTheme.Value.Theme);
    }

    private Task ApplyTheme(Theme theme)
        => Dispatcher.InvokeAsync(async () => {
            _whenReadySource.TrySetResult(default);
            if (!HostInfo.IsDevelopmentInstance) // Themes work on dev instances only
                return;
            if (_appliedTheme == theme)
                return;

            _appliedTheme = theme;
            try {
                await JS.InvokeVoidAsync($"{BlazorUICoreModule.ImportName}.ThemeUI.applyTheme", theme.ToString());
            }
            catch (Exception e) when (e is not OperationCanceledException) {
                Log.LogError(e, "Failed to apply the new theme");
            }
        });
}
