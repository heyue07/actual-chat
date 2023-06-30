using Stl.Diagnostics;

namespace ActualChat.UI.Blazor.Services;

public enum AutoNavigationReason
{
    Unknown = 0,
    SignIn = 1,
    Notification = 10,
}

public abstract class AutoNavigationUI : IHasServices
{
    private AppBlazorCircuitContext? _blazorCircuitContext;
    private List<(LocalUrl Url, AutoNavigationReason Reason)>? _autoNavigationCandidates = new();

    protected ILogger Log { get; }
    protected ILogger? DebugLog { get; }

    public IServiceProvider Services { get; }
    public History History { get; }
    public AppBlazorCircuitContext BlazorCircuitContext => _blazorCircuitContext ??= Services.GetRequiredService<AppBlazorCircuitContext>();
    public Dispatcher Dispatcher => BlazorCircuitContext.Dispatcher;

    public bool? InitialLeftPanelIsVisible { get; set; }

    protected AutoNavigationUI(IServiceProvider services)
    {
        Services = services;
        Log = services.LogFor(GetType());
        History = services.GetRequiredService<History>();
        DebugLog = Log.IfEnabled(LogLevel.Debug);
    }

    public async ValueTask<LocalUrl> GetAutoNavigationUrl(CancellationToken cancellationToken)
    {
        Dispatcher.AssertAccess();
        var candidateUrl = (LocalUrl?)null;
        if (_autoNavigationCandidates == null)
            throw StandardError.Internal($"{nameof(GetAutoNavigationUrl)} is called twice.");

        if (_autoNavigationCandidates.Count > 0)
            candidateUrl = _autoNavigationCandidates.MaxBy(t => (int) t.Reason).Url;
        _autoNavigationCandidates = null;

        var currentUrl = History.LocalUrl;
        var url = candidateUrl ?? await GetDefaultAutoNavigationUrl().ConfigureAwait(false);
        url = await FixUrl(url, cancellationToken).ConfigureAwait(false);
        if (url != currentUrl && url.IsChat()) {
            // Original URL was either home or chat root page,
            // so left panel must be open when PanelsUI is loaded
            InitialLeftPanelIsVisible = true;
        }
        return url;
    }

    public Task DispatchNavigateTo(LocalUrl url, AutoNavigationReason reason)
    {
        DebugLog?.LogDebug("DispatchNavigateTo({Url}, {Reason})", url, reason);
        if (BlazorCircuitContext.WhenReady.IsCompleted)
            return Dispatcher.CheckAccess()
                ? NavigateTo(url, reason)
                : Dispatcher.InvokeAsync(() => NavigateTo(url, reason));

        return Task.Run(async () => {
            await BlazorCircuitContext.WhenReady.ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => NavigateTo(url, reason));
        });
    }

    public Task NavigateTo(LocalUrl url, AutoNavigationReason reason)
    {
        Dispatcher.AssertAccess();
        if (_autoNavigationCandidates == null)
            return FixUrlAndNavigate(); // Initial navigation already happened

        // Initial navigation haven't happened yet
        DebugLog?.LogDebug("NavigateTo({Url}, {Reason}): auto navigation candidate is added", url, reason);
        _autoNavigationCandidates.Add((url, reason));
        return History.WhenReady;

        async Task FixUrlAndNavigate()
        {
            DebugLog?.LogDebug("NavigateTo({Url}, {Reason}): processing", url, reason);
            var cancellationToken = BlazorCircuitContext.StopToken;
            url = await FixUrl(url, cancellationToken).ConfigureAwait(true);
            await History.NavigateTo(url).SilentAwait();
        }
    }

    // Protected methods

    protected abstract ValueTask<LocalUrl> GetDefaultAutoNavigationUrl();
    protected abstract ValueTask<LocalUrl> FixUrl(LocalUrl url, CancellationToken cancellationToken);
}
