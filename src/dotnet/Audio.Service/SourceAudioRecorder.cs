using ActualChat.Audio.Db;
using ActualChat.Chat;
using ActualChat.Redis;
using Stl.Redis;

namespace ActualChat.Audio;

public class SourceAudioRecorder : ISourceAudioRecorder, IAsyncDisposable
{
    private readonly IChatAuthorsBackend _chatAuthorsBackend;
    private readonly RedisDb _redisDb;
    private readonly RedisQueue<AudioRecord> _newRecordQueue;
    private readonly MomentClockSet _clocks;
    private readonly ILogger<SourceAudioRecorder> _log;

    public SourceAudioRecorder(
        RedisDb<AudioContext> audioRedisDb,
        IChatAuthorsBackend chatAuthorsBackend,
        MomentClockSet clocks,
        ILogger<SourceAudioRecorder> log)
    {
        _log = log;
        _clocks = clocks;
        _redisDb = audioRedisDb.WithKeyPrefix("source-audio");
        _newRecordQueue = _redisDb.GetQueue<AudioRecord>("new-records");
        _chatAuthorsBackend = chatAuthorsBackend;
    }

    public ValueTask DisposeAsync()
        => _newRecordQueue.DisposeAsync();

    public async Task RecordSourceAudio(
        Session session,
        AudioRecord record,
        IAsyncEnumerable<BlobPart> blobStream,
        CancellationToken cancellationToken)
    {
        var author = await _chatAuthorsBackend.GetOrCreate(session, record.ChatId, cancellationToken).ConfigureAwait(false);
        record = record with {
            Id = new AudioRecordId(Ulid.NewUlid().ToString()),
            AuthorId = author.Id,
        };
        _log.LogInformation("RecordSourceAudio: Record = {Record}", record);

        var streamer = _redisDb.GetStreamer<BlobPart>(record.Id);
        if (Constants.DebugMode.AudioRecordingBlobStream)
            blobStream = blobStream.WithLog(_log, "RecordSourceAudio", cancellationToken);
        await streamer.Write(
                blobStream,
                _ => _newRecordQueue.Enqueue(record).ToValueTask(),
                cancellationToken)
            .ConfigureAwait(false);
        _ = BackgroundTask.Run(DelayedStreamerRemoval,
            _log, $"{nameof(DelayedStreamerRemoval)} failed",
            CancellationToken.None);

        async Task DelayedStreamerRemoval()
        {
            await _clocks.CpuClock.Delay(TimeSpan.FromMinutes(1), CancellationToken.None).ConfigureAwait(false);
            await streamer.Remove().ConfigureAwait(false);
        }
    }


    public Task<AudioRecord> DequeueSourceAudio(CancellationToken cancellationToken)
        => _newRecordQueue.Dequeue(cancellationToken);

    public IAsyncEnumerable<BlobPart> GetSourceAudioBlobStream(AudioRecordId audioRecordId, CancellationToken cancellationToken)
    {
        var streamer = _redisDb.GetStreamer<BlobPart>(audioRecordId);
        return streamer.Read(cancellationToken);
    }
}
