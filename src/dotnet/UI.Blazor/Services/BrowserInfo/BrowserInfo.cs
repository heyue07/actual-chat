using ActualChat.Hosting;
using ActualChat.UI.Blazor.Module;

namespace ActualChat.UI.Blazor.Services;

public class BrowserInfo : IBrowserInfoBackend, IDisposable
{
    private readonly IMutableState<ScreenSize> _screenSize;
    private readonly IMutableState<bool> _isHoverable;
    private readonly IMutableState<bool> _isVisible;

    protected readonly TaskCompletionSource<Unit> WhenReadySource = TaskCompletionSourceExt.New<Unit>();
    protected DotNetObjectReference<IBrowserInfoBackend>? BackendRef;
    protected readonly object Lock = new();

    protected IServiceProvider Services { get; }
    protected HostInfo HostInfo { get; }
    protected IJSRuntime JS { get; }
    protected UICommander UICommander { get; }
    protected ILogger Log { get; }

    public AppKind AppKind { get; }
    // ReSharper disable once InconsistentlySynchronizedField
    public IState<ScreenSize> ScreenSize => _screenSize;
    public IState<bool> IsHoverable => _isHoverable;
    public IState<bool> IsVisible => _isVisible;
    public TimeSpan UtcOffset { get; protected set; }
    public bool IsMobile { get; protected set; }
    public bool IsAndroid { get; protected set; }
    public bool IsIos { get; protected set; }
    public bool IsChromium { get; protected set; }
    public bool IsEdge { get; protected set; }
    public bool IsWebKit { get; protected set; }
    public bool IsTouchCapable { get; protected set; }
    public string WindowId { get; protected set; } = "";
    public Task WhenReady => WhenReadySource.Task;

    public BrowserInfo(IServiceProvider services)
    {
        Services = services;
        Log = services.LogFor(GetType());

        HostInfo = services.GetRequiredService<HostInfo>();
        JS = services.GetRequiredService<IJSRuntime>();
        UICommander = services.GetRequiredService<UICommander>();
        AppKind = HostInfo.AppKind;

        var stateFactory = services.StateFactory();
        _screenSize = stateFactory.NewMutable<ScreenSize>();
        _isHoverable = stateFactory.NewMutable(false);
        _isVisible = stateFactory.NewMutable(true);
    }

    public void Dispose()
        => BackendRef.DisposeSilently();

    public virtual ValueTask Initialize(List<object?>? initCalls = null)
    {
        var jsMethod = $"{BlazorUICoreModule.ImportName}.BrowserInfo.init";
        BackendRef = DotNetObjectReference.Create<IBrowserInfoBackend>(this);
        if (initCalls != null) {
            initCalls.Add(jsMethod);
            initCalls.Add(2);
            initCalls.Add(BackendRef);
            initCalls.Add(AppKind.ToString());
            return default;
        }

        return JS.InvokeVoidAsync(jsMethod, BackendRef, AppKind.ToString());
    }

    [JSInvokable]
    public virtual void OnInitialized(IBrowserInfoBackend.InitResult initResult)
    {
        Log.LogDebug("OnInitialized: {InitResult}", initResult);

        if (!Enum.TryParse<ScreenSize>(initResult.ScreenSizeText, true, out var screenSize))
            screenSize = Blazor.Services.ScreenSize.Unknown;

        Update(screenSize, initResult.IsHoverable, initResult.IsVisible);
        UtcOffset = TimeSpan.FromMinutes(initResult.UtcOffset);
        IsMobile = initResult.IsMobile;
        IsAndroid = initResult.IsAndroid;
        IsIos = initResult.IsIos;
        IsChromium = initResult.IsChromium;
        IsEdge = initResult.IsEdge;
        IsWebKit = initResult.IsWebKit;
        IsTouchCapable = initResult.IsTouchCapable;
        WindowId = initResult.WindowId;
        WhenReadySource.TrySetResult(default);
    }

    [JSInvokable]
    public void OnScreenSizeChanged(string screenSizeText, bool isHoverable)
    {
        if (!Enum.TryParse<ScreenSize>(screenSizeText, true, out var screenSize))
            screenSize = Blazor.Services.ScreenSize.Unknown;
        Update(screenSize, isHoverable);
    }

    [JSInvokable]
    public void OnIsVisibleChanged(bool isVisible)
        => Update(isVisible: isVisible);

    // Protected methods

    protected void Update(ScreenSize? screenSize = null, bool? isHoverable = null, bool? isVisible = null)
    {
        var isUpdated = false;
 #pragma warning disable MA0064
        lock (Lock) {
 #pragma warning restore MA0064
            if (screenSize is { } vScreenSize && _screenSize.Value != vScreenSize) {
                _screenSize.Value = vScreenSize;
                isUpdated = true;
            }
            if (isHoverable is { } vIsHoverable && _isHoverable.Value != vIsHoverable) {
                _isHoverable.Value = vIsHoverable;
                isUpdated = true;
            }
            if (isVisible is { } vIsVisible && _isVisible.Value != vIsVisible) {
                _isVisible.Value = vIsVisible;
                isUpdated = true;
            }
        }
        if (isUpdated)
            _ = UICommander.RunNothing(); // To instantly update everything
    }
}
