using ActualChat.App.Maui.Services;
using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;
using Stl.Internal;

namespace ActualChat.App.Maui;

public class AppServicesAccessor
{
    private static readonly object _lock = new();
    private static ILogger? _log;
    private static volatile IServiceProvider? _appServices;
    private static volatile IServiceProvider? _scopedServices;
    private static volatile TaskCompletionSource<IServiceProvider> _scopedServicesTask =
        TaskCompletionSourceExt.New<IServiceProvider>();

    private static ILogger Log => _log ??= MauiDiagnostics.LoggerFactory.CreateLogger<AppServicesAccessor>();

    public static Task<IServiceProvider> ScopedServicesTask => _scopedServicesTask.Task;

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
                Log.LogDebug("AppServices ready");
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
                _scopedServicesTask.TrySetResult(value);
                Log.LogDebug("ScopedServices ready");
            }
        }
    }

    public static bool TryGetScopedServices([NotNullWhen(true)] out IServiceProvider? scopedServices)
    {
        scopedServices = _scopedServices;
        return scopedServices != null;
    }

    public static void DiscardScopedServices(IServiceProvider? expectedScopedServices = null)
    {
        lock (_lock) {
            var scopedServices = _scopedServices;
            if (scopedServices == null)
                return;
            if (expectedScopedServices != null && !ReferenceEquals(scopedServices, expectedScopedServices))
                return;

            _scopedServicesTask.TrySetCanceled();
            _scopedServicesTask = TaskCompletionSourceExt.New<IServiceProvider>(); // Must go first
            _scopedServices = null;
            try {
                if (scopedServices.GetService<IJSRuntime>() is SafeJSRuntime js)
                    js.MarkDisconnected();
            }
            catch {
                // Intended
            }
        }
        AppServices.LogFor(nameof(AppServicesAccessor)).LogDebug("ScopedServices discarded");
    }
}
