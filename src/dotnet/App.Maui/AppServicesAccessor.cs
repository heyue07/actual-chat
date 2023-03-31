using ActualChat.App.Maui.Services;
using Microsoft.JSInterop;
using Stl.Internal;

namespace ActualChat.App.Maui;

public static class AppServicesAccessor
{
    private static readonly object _lock = new();
    private static volatile IServiceProvider? _appServices;
    private static volatile IServiceProvider? _scopedServices;
    private static volatile Task<Unit> _whenScopedServicesReady = TaskSource.New<Unit>(true).Task;

    public static bool AreScopedServicesReady => _scopedServices != null;
    public static Task WhenScopedServicesReady => _whenScopedServicesReady;

    public static IServiceProvider AppServices {
        get => _appServices ?? throw Errors.NotInitialized(nameof(AppServices));
        set {
            lock (_lock) {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (ReferenceEquals(_appServices, value))
                    return;
                if (_appServices != null)
                    throw Errors.AlreadyInitialized(nameof(AppServices));

                _appServices = value;
                AppServices.LogFor(nameof(AppServicesAccessor)).LogDebug("AppServices ready");
            }
        }
    }

    public static IServiceProvider ScopedServices {
        get => _scopedServices ?? throw Errors.NotInitialized(nameof(ScopedServices));
        set {
            lock (_lock) {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (ReferenceEquals(_scopedServices, value))
                    return;
                if (_scopedServices != null)
                    throw Errors.AlreadyInitialized(nameof(ScopedServices));

                _scopedServices = value;
                TaskSource.For(_whenScopedServicesReady).TrySetResult(default);
                AppServices.LogFor(nameof(AppServicesAccessor)).LogDebug("ScopedServices ready");
            }
        }
    }

    public static void DiscardScopedServices()
    {
        lock (_lock) {
            if (_scopedServices == null)
                return;

            var js = _scopedServices.GetRequiredService<IJSRuntime>();
            JSObjectReferenceDisconnectHelper.MarkAsDisconnected(js);
            _scopedServices = null;
            _whenScopedServicesReady = TaskSource.New<Unit>(true).Task;
            AppServices.LogFor(nameof(AppServicesAccessor)).LogDebug("ScopedServices discarded");
        }
    }
}
