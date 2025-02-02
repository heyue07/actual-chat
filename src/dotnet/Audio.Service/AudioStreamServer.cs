namespace ActualChat.Audio;

public class AudioStreamServer : StreamServerBase<byte[]>, IAudioStreamServer
{
    public AudioStreamServer(IServiceProvider services) : base(services)
    { }

    public virtual async Task<IAsyncEnumerable<byte[]>> Read(
        Symbol streamId,
        TimeSpan skipTo,
        CancellationToken cancellationToken)
    {
        var stream = await Read(streamId, cancellationToken).ConfigureAwait(false);
        return SkipTo(stream, skipTo, cancellationToken);
    }

    public new virtual Task Write(Symbol streamId, IAsyncEnumerable<byte[]> stream, CancellationToken cancellationToken)
        => base.Write(streamId, stream, cancellationToken);

    public AudioStreamServer SkipDispose()
        => new SkipDisposeWrapper(this);

    // Private methods

    private static IAsyncEnumerable<byte[]> SkipTo(
        IAsyncEnumerable<byte[]> audioStream,
        TimeSpan skipTo,
        CancellationToken cancellationToken)
    {
        // This method assumes there are 20ms packets!
        // And the first packet is the header
        if (skipTo <= TimeSpan.Zero)
            return audioStream;

        var skipToFrameN = (int)skipTo.TotalMilliseconds / 20;
        var (headerDataTask, dataStream) = audioStream.SplitHead(cancellationToken);
        return dataStream
            .SkipWhile((_, i) => i < skipToFrameN)
            .Prepend(headerDataTask);
    }

    private sealed class SkipDisposeWrapper(AudioStreamServer instance) : AudioStreamServer(instance.Services)
    {
        public override Task<IAsyncEnumerable<byte[]>> Read(Symbol streamId, TimeSpan skipTo, CancellationToken cancellationToken)
            => instance.Read(streamId, skipTo, cancellationToken);

        public override Task Write(Symbol streamId, IAsyncEnumerable<byte[]> stream, CancellationToken cancellationToken)
            => instance.Write(streamId, stream, cancellationToken);

        public override void Dispose()
        { }
    }
}
