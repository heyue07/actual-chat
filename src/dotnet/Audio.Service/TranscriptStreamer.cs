using ActualChat.Redis;
using ActualChat.Transcription;

namespace ActualChat.Audio;

public class TranscriptStreamer : ITranscriptStreamer
{
    private readonly ILogger<TranscriptStreamer> _log;
    private readonly RedisDb _redisDb;

    public TranscriptStreamer(
        RedisDb rootRedisDb,
        ILogger<TranscriptStreamer> log)
    {
        _log = log;
        _redisDb = rootRedisDb.WithKeyPrefix("transcripts");
    }

    public Task PublishTranscriptStream(StreamId streamId, ChannelReader<TranscriptUpdate> transcriptUpdates, CancellationToken cancellationToken)
    {
        var streamer = _redisDb.GetStreamer<TranscriptUpdate>(streamId);
        return streamer.Write(transcriptUpdates, cancellationToken);
    }

    public Task<ChannelReader<TranscriptUpdate>> GetTranscriptStream(StreamId streamId, CancellationToken cancellationToken)
    {
        var streamer = _redisDb.GetStreamer<TranscriptUpdate>(streamId);
        return Task.FromResult(streamer.Read(cancellationToken));
    }
}
