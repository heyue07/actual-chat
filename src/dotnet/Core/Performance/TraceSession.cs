namespace ActualChat.Performance;

public sealed class TraceSession : ITraceSession
{
    private readonly Stopwatch _stopwatch;
    private Action<string> _output;

    public static TraceSession Main { get; } = new ("main");
    public static NullTraceSession Null => NullTraceSession.Instance;

    public static TraceSession New(string name)
        => new (name);

    private TraceSession(string name)
    {
        if (name.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(name));
        Name = name;
        _stopwatch = new Stopwatch();
        _output = DefaultOutput;
    }

    public string Name { get; }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public bool IsStarted => _stopwatch.IsRunning;

    public TraceSession ConfigureOutput(Action<string> output)
    {
        _output = output;
        return this;
    }

    public TraceSession Start()
    {
        _stopwatch.Start();
        Track("Started");
        return this;
    }

    public void Track(string message)
    {
        var ts = _stopwatch.Elapsed;
        var tid = Thread.CurrentThread.ManagedThreadId;
        var formattedMessage = $"Trace [{Name}] [{tid:000}] {ts:c} {message}";
        _output(formattedMessage);
    }

    public TraceStep TrackStep(string message)
    {
        var interval = new TraceStep(this, message);
        Track(message);
        return interval;
    }

    private static void DefaultOutput(string formattedMessage)
        => Debug.WriteLine(formattedMessage);
}
