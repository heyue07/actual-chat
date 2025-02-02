using ActualChat.UI.Blazor.Module;

namespace ActualChat.UI.Blazor.Services;

public class KeepAwakeUI(IServiceProvider services)
{
    private static readonly string JSSetKeepAwakeMethod = $"{BlazorUICoreModule.ImportName}.KeepAwakeUI.setKeepAwake";

    private IJSRuntime? _js;
    private ILogger? _log;

    protected IServiceProvider Services { get; } = services;
    protected IJSRuntime JS => _js ??= Services.JSRuntime();
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    public virtual ValueTask SetKeepAwake(bool value)
    {
        Log.LogDebug("SetKeepAwake({MustKeepAwake})", value);
        return JS.InvokeVoidAsync(JSSetKeepAwakeMethod, value);
    }
}
