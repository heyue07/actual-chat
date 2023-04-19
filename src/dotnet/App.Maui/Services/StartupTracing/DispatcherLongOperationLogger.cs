namespace ActualChat.App.Maui.Services.StartupTracing;

internal class DispatcherLongOperationLogger : IDispatcherOperationLogger
{
    private const int ShortTasksBatchSize = 30;
    private static readonly TimeSpan _longWorkItemThreshold = TimeSpan.FromMilliseconds(10);
    private Stopwatch _sw = new ();
    private long _shortWorkItemNumber;
    private TimeSpan _showWorkItemTotalDuration;
    private readonly Tracer _tracer;

    public DispatcherLongOperationLogger()
        => _tracer = Tracer.Default["Dispatcher"];

    public void OnBeforeOperation()
        => _sw = Stopwatch.StartNew();

    public void OnAfterOperation()
    {
        _sw.Stop();
        var elapsed = _sw.Elapsed;
        var isLongOperation = elapsed >= _longWorkItemThreshold;
        if (!isLongOperation) {
            _shortWorkItemNumber++;
            _showWorkItemTotalDuration += elapsed;
        }
        if (_shortWorkItemNumber >= ShortTasksBatchSize || isLongOperation) {
            _tracer.Point(
                $"Short tasks duration: {TracePoint.FormatDuration(_showWorkItemTotalDuration)} ({_shortWorkItemNumber} tasks)");
            _shortWorkItemNumber = 0;
            _showWorkItemTotalDuration = TimeSpan.Zero;
        }
        if (isLongOperation) {
            var startTime = DateTime.Now - elapsed;
            _tracer.Point(
                $"Long task duration: {TracePoint.FormatDuration(elapsed)}, estimated start time: '{startTime:HH:mm:ss.fff}'");
        }
    }
}
