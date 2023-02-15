using ActualChat.Hosting;
using ActualChat.Kvas;
using ActualChat.UI.Blazor.Module;

namespace ActualChat.UI.Blazor.Services;

public sealed class BrowserInfo : IBrowserInfoBackend, IOriginProvider, IDisposable
{
    private DotNetObjectReference<IBrowserInfoBackend>? _backendRef;
    private readonly IMutableState<ScreenSize> _screenSize;
    private readonly IMutableState<bool> _isHoverable;
    private readonly TaskSource<Unit> _whenReadySource;
    private bool _hardRedirectCompleted;
    private readonly object _lock = new();

    private IServiceProvider Services { get; }
    private ILogger Log { get; }

    private HostInfo HostInfo { get; }
    private HistoryUI HistoryUI { get; }
    private UrlMapper UrlMapper { get; }
    private IJSRuntime JS { get; }
    private UICommander UICommander { get; }

    public AppKind AppKind { get; }
    // ReSharper disable once InconsistentlySynchronizedField
    public IState<ScreenSize> ScreenSize => _screenSize;
    public IState<bool> IsHoverable => _isHoverable;
    public TimeSpan UtcOffset { get; private set; }
    public bool IsMobile { get; private set; }
    public bool IsAndroid { get; private set; }
    public bool IsIos { get; private set; }
    public bool IsChrome { get; private set; }
    public bool IsTouchCapable { get; private set; }
    public string WindowId { get; private set; } = "";
    public Task WhenReady => _whenReadySource.Task;
    string IOriginProvider.Origin => WindowId;

    public BrowserInfo(IServiceProvider services)
    {
        Services = services;
        Log = services.LogFor(GetType());

        HostInfo = services.GetRequiredService<HostInfo>();
        HistoryUI = services.GetRequiredService<HistoryUI>();
        UrlMapper = services.GetRequiredService<UrlMapper>();
        JS = services.GetRequiredService<IJSRuntime>();
        UICommander = services.GetRequiredService<UICommander>();
        AppKind = HostInfo.AppKind;

        var stateFactory = services.StateFactory();
        _screenSize = stateFactory.NewMutable<ScreenSize>();
        _isHoverable = stateFactory.NewMutable(false);
        _whenReadySource = TaskSource.New<Unit>(true);
    }

    public void Dispose()
        => _backendRef.DisposeSilently();

    public async Task Init()
    {
        _backendRef = DotNetObjectReference.Create<IBrowserInfoBackend>(this);
        await JS.InvokeVoidAsync(
            $"{BlazorUICoreModule.ImportName}.BrowserInfo.init",
            _backendRef,
            AppKind.ToString());
    }

    public ValueTask HardRedirect(LocalUrl url)
        => HardRedirect(url.ToAbsolute(UrlMapper));

    public async ValueTask HardRedirect(string url)
    {
        lock (_lock) {
            if (_hardRedirectCompleted)
                return;

            // Set it preemptively to prevent concurrent hard redirects;
            // we'll reset this value in case of an error.
            _hardRedirectCompleted = true;
        }
        try {
            Log.LogInformation("HardRedirect: -> '{Url}'", url);
            await JS.InvokeVoidAsync(
                $"{BlazorUICoreModule.ImportName}.BrowserInfo.hardRedirect",
                url);
        }
        catch (Exception e) {
            lock (_lock)
                _hardRedirectCompleted = false;
            Log.LogError(e, "HardRedirect failed");
            throw;
        }
    }

    [JSInvokable]
    public void OnInitialized(IBrowserInfoBackend.InitResult initResult) {
        // Log.LogInformation("Init: {InitResult}", initResult);
        SetScreenSize(initResult.ScreenSizeText, initResult.IsHoverable);
        UtcOffset = TimeSpan.FromMinutes(initResult.UtcOffset);
        IsMobile = initResult.IsMobile;
        IsAndroid = initResult.IsAndroid;
        IsIos = initResult.IsIos;
        IsChrome = initResult.IsChrome;
        IsTouchCapable = initResult.IsTouchCapable;
        WindowId = initResult.WindowId;
        _whenReadySource.SetResult(default);
    }

    [JSInvokable]
    public void OnScreenSizeChanged(string screenSizeText, bool isHoverable)
        => SetScreenSize(screenSizeText, isHoverable);

    private void SetScreenSize(string screenSizeText, bool isHoverable)
    {
        if (!Enum.TryParse<ScreenSize>(screenSizeText, true, out var screenSize))
            screenSize = Blazor.Services.ScreenSize.Unknown;
        // Log.LogInformation("ScreenSize = {ScreenSize}", screenSize);

        bool mustSaveHistory;
        lock (_lock) {
            if (_screenSize.Value == screenSize && _isHoverable.Value == isHoverable)
                return;

            mustSaveHistory =
                _screenSize.Value.IsNarrow() != screenSize.IsNarrow()
                || _isHoverable.Value != isHoverable;
            _screenSize.Value = screenSize;
            _isHoverable.Value = isHoverable;
        }
        if (mustSaveHistory)
            HistoryUI.Save(); // Some states depend on ScreenSize.IsNarrow / IsWide, or IsHoverable
        UICommander.RunNothing(); // To instantly update everything
    }
}
