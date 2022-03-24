using System.Reflection;
using ActualChat.Audio.UI.Blazor.Module;
using ActualChat.Media;
using ActualChat.MediaPlayback;
using Microsoft.Extensions.Hosting;

namespace ActualChat.Audio.UI.Blazor.Components;

public class AudioTrackPlayer : TrackPlayer, IAudioPlayerBackend
{
    private static bool _debugMode => Constants.DebugMode.AudioPlayback;

    private readonly string _id;
    private readonly BlazorCircuitContext _circuitContext;
    private readonly IJSRuntime _js;
    private readonly ILogger<AudioTrackPlayer> _log;
    private readonly ILogger<AudioTrackPlayer>? _debugLog;
    private DotNetObjectReference<IAudioPlayerBackend>? _blazorRef;
    private IJSObjectReference? _jsRef;
    private Task<Unit> _whenBufferReady = TaskSource.New<Unit>(true).Task;
    private bool _isStopSent;

    public AudioTrackPlayer(
        string id,
        IMediaSource source,
        IHostApplicationLifetime lifetime,
        BlazorCircuitContext circuitContext,
        IJSRuntime jsRuntime,
        ILogger<AudioTrackPlayer> log
    ) : base(lifetime, source, log)
    {
        _id = id;
        _circuitContext = circuitContext;
        _js = jsRuntime;
        _log = log;
        _debugLog = _debugMode ? _log : null;
        UpdateBufferReadyState(true);
    }

    [JSInvokable]
    public Task OnPlaybackEnded(string? errorMessage)
    {
        Exception? error = null;
        if (errorMessage != null) {
            error = new TargetInvocationException(
                $"[AudioTrackPlayer #{_id}] Playback stopped with an error, message = '{errorMessage}'.",
                null);
            _log.LogError(error, "[AudioTrackPlayer #{AudioTrackPlayerId}] Playback stopped with an error", _id);
        }
        _debugLog?.LogDebug("[AudioTrackPlayer #{AudioTrackPlayerId}] OnPlaybackEnded: {Message}", _id, errorMessage);
        OnStopped(error);
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnPlaybackTimeChanged(double offset)
    {
        _debugLog?.LogDebug("[AudioTrackPlayer #{AudioTrackPlayerId}] OnPlayedTo: {Offset}", _id, offset);
        OnPlayedTo(TimeSpan.FromSeconds(offset));
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnChangeReadiness(bool isBufferReady)
    {
        UpdateBufferReadyState(isBufferReady);
        return Task.CompletedTask;
    }

    protected override async ValueTask ProcessCommand(IPlayerCommand command, CancellationToken cancellationToken)
        => await CircuitInvoke(
            async () => {
                switch (command) {
                    case PlayCommand:
                        if (_jsRef != null)
                            throw new LifetimeException($"[AudioTrackPlayer #{_id}] Double start playback command");
                        _blazorRef = DotNetObjectReference.Create<IAudioPlayerBackend>(this);
                        _debugLog?.LogDebug("[AudioTrackPlayer #{AudioTrackPlayerId}] Create audio player in js", _id);
                        _jsRef = await _js.InvokeAsync<IJSObjectReference>(
                                    $"{AudioBlazorUIModule.ImportName}.AudioPlayer.create",
                                    CancellationToken.None,
                                    _blazorRef,
                                    _debugMode,
                                    _id
                                ).ConfigureAwait(true);
                        break;
                    case StopCommand:
                        if (!_isStopSent) {
                            if (_jsRef == null)
                                throw new LifetimeException($"[AudioTrackPlayer #{_id}] {nameof(StopCommand)}: Start command should be called first.");
                            _debugLog?.LogDebug("[AudioTrackPlayer #{AudioTrackPlayerId}] Send stop command to js", _id);
                            _ = _jsRef.InvokeVoidAsync("stop", CancellationToken.None);
                            _isStopSent = true;
                        }
                        break;
                    case EndCommand:
                        if (_jsRef == null)
                            throw new LifetimeException($"[AudioTrackPlayer #{_id}] {nameof(EndCommand)}: Start command should be called first.");
                        _debugLog?.LogDebug("[AudioTrackPlayer #{AudioTrackPlayerId}] Send end command to js", _id);
                        _ = _jsRef.InvokeVoidAsync("end", CancellationToken.None);
                        break;
                    default:
                        throw new NotSupportedException($"[AudioTrackPlayer #{_id}] Unsupported command type: '{command.GetType()}'.");
                }
            }).ConfigureAwait(false);

    protected override async ValueTask ProcessMediaFrame(MediaFrame frame, CancellationToken cancellationToken)
        => await CircuitInvoke(
            async () => {
                if (_jsRef == null)
                    throw new LifetimeException($"[AudioTrackPlayer #{_id}] Can't process media frame before initialization.");

                var chunk = frame.Data;
                _ = _jsRef.InvokeVoidAsync("data", cancellationToken, chunk);
                try {
                    await _whenBufferReady.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException) {
                    _log.LogError(
                        "[AudioTrackPlayer #{AudioTrackPlayerId}] ProcessMediaFrame: ready-to-buffer wait timed out, offset={FrameOffset}",
                        _id,
                        frame.Offset);
                }
            }).ConfigureAwait(false);

    protected override Task PlayInternal(CancellationToken cancellationToken)
        => base.PlayInternal(cancellationToken).ContinueWith(async _ => await CircuitInvoke(async () => {
            var (jsRef, blazorRef) = (_jsRef, _blazorRef);
            (_jsRef, _blazorRef) = (null, null);
            try {
                try {
                    if (jsRef != null)
                        await jsRef.DisposeAsync().ConfigureAwait(true);
                }
                finally {
                    blazorRef?.Dispose();
                }
            }
            catch (Exception ex) {
                _log.LogError(ex, "[AudioTrackPlayer #{AudioTrackPlayerId}] OnStopped failed while disposing the references", _id);
            }
        }).ConfigureAwait(false),
        CancellationToken.None,
        TaskContinuationOptions.RunContinuationsAsynchronously,
        TaskScheduler.Default
    );

    private Task CircuitInvoke(Func<Task> workItem)
        => CircuitInvoke(async () => { await workItem().ConfigureAwait(false); return true; });
#pragma warning disable RCS1229
    private Task<TResult?> CircuitInvoke<TResult>(Func<Task<TResult?>> workItem)
    {
        try {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            return _circuitContext.IsDisposing || _circuitContext.RootComponent == null
                ? Task.FromResult(default(TResult?))
                : _circuitContext.RootComponent.GetDispatcher().InvokeAsync(workItem);
        }
        catch (Exception e) when (e is not OperationCanceledException) {
            _log.LogError(e, $"[AudioTrackPlayer #{{AudioTrackPlayerId}}] {nameof(CircuitInvoke)} failed", _id);
            throw;
        }
    }

    private void UpdateBufferReadyState(bool isBufferReady)
    {
        if (isBufferReady) {
            if (_whenBufferReady.IsCompleted)
                return;
            TaskSource.For(_whenBufferReady).TrySetResult(default);
        }
        else {
            if (!_whenBufferReady.IsCompleted)
                return;
            _whenBufferReady = TaskSource.New<Unit>(true).Task;
        }
    }
}
