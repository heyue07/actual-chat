namespace ActualChat.Channels;

public static class ChannelExt
{
    private static readonly ChannelClosedException ChannelClosedError = new();

    public static UnboundedChannelOptions SingleReaderWriterUnboundedChannelOptions { get; } = new () {
        SingleReader = true,
        SingleWriter = true,
    };

    public static AsyncMemoizer<T> Memoize<T>(
        this Channel<T> source,
        CancellationToken cancellationToken = default)
        => new(source.Reader.ReadAllAsync(cancellationToken), cancellationToken);
    public static AsyncMemoizer<T> Memoize<T>(
        this ChannelReader<T> source,
        CancellationToken cancellationToken = default)
        => new(source.ReadAllAsync(cancellationToken), cancellationToken);

    public static async ValueTask<Option<T>> TryReadAsync<T>(
        this ChannelReader<T> channel,
        CancellationToken cancellationToken = default)
    {
        while (await channel.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        while (channel.TryRead(out var value))
            return value;
        return Option<T>.None;
    }

    public static bool TryReadResult<T>(
        this ChannelReader<T> channel,
        out Result<T> result)
    {
        try {
            if (!channel.TryRead(out var value)) {
                result = default;
                return false;
            }
            result = value;
            return true;
        }
        catch (Exception e)  when (e is not OperationCanceledException) {
            result = Result.New<T>(default!, e);
            return true;
        }
    }

    public static async ValueTask<Result<T>> ReadResultAsync<T>(
        this ChannelReader<T> channel,
        CancellationToken cancellationToken = default)
    {
        try {
            while (await channel.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            while (channel.TryRead(out var value)) {
                return value;
            }
            return GetChannelClosedResult<T>();
        }
        catch (Exception e) when (e is not OperationCanceledException) {
            return Result.New<T>(default!, e);
        }
    }

    public static async ValueTask WriteResultAsync<T>(
        this ChannelWriter<T> channel,
        Result<T> result,
        CancellationToken cancellationToken = default)
    {
        if (result.IsValue(out var value))
            await channel.WriteAsync(value, cancellationToken).ConfigureAwait(false);
        else {
            var error = result.Error;
            if (error is ChannelClosedException)
                channel.TryComplete();
            else
                channel.TryComplete(error);
        }
    }

    public static Result<T> GetChannelClosedResult<T>()
        => Result.New<T>(default!, ChannelClosedError);
}
