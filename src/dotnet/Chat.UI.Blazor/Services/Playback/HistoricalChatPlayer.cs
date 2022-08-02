namespace ActualChat.Chat.UI.Blazor.Services;

public sealed class HistoricalChatPlayer : ChatPlayer
{
    public HistoricalChatPlayer(Session session, Symbol chatId, IServiceProvider services)
        : base(session, chatId, services)
        => PlayerKind = ChatPlayerKind.Historical;

    protected override async Task Play(
        ChatEntryPlayer entryPlayer, Moment startAt, CancellationToken cancellationToken)
    {
        var cpuClock = Clocks.CpuClock;
        var audioEntryReader = Chats.NewEntryReader(Session, ChatId, ChatEntryType.Audio);
        var idRange = await Chats.GetIdRange(Session, ChatId, ChatEntryType.Audio, cancellationToken)
            .ConfigureAwait(false);
        var startEntry = await audioEntryReader
            .FindByMinBeginsAt(startAt - Constants.Chat.MaxEntryDuration, idRange, cancellationToken)
            .ConfigureAwait(false);
        if (startEntry == null) {
            Log.LogWarning("Couldn't find start entry");
            return;
        }

        var playbackBlockEnd = cpuClock.Now - TimeSpan.FromDays(1); // Any time in past
        var playbackOffset = playbackBlockEnd - Moment.EpochStart; // now - playTime

        idRange = (startEntry.Id, idRange.End);
        var entries = audioEntryReader.Read(idRange, cancellationToken);
        await foreach (var entry in entries.ConfigureAwait(false)) {
            if (!entry.StreamId.IsEmpty) // Streaming entry
                continue;
            if (entry.EndsAt < startAt)
                // We're normally starting @ (startAt - ChatConstants.MaxEntryDuration),
                // so we need to skip a few entries.
                continue;

            var now = cpuClock.Now;
            var entryBeginsAt = Moment.Max(entry.BeginsAt, startAt);
            var entryEndsAt = entry.EndsAt ?? entry.BeginsAt + InfDuration;
            entryEndsAt = Moment.Min(entryEndsAt, entry.ContentEndsAt ?? entryEndsAt);
            var skipTo = entryBeginsAt - entry.BeginsAt;
            if (playbackBlockEnd < entryBeginsAt + playbackOffset) {
                // There is a gap between the currently playing "block" and the entry.
                // This means we're still playing the "historical" block, and the new entry
                // starts with some gap after it; we're going to nullify this gap here by
                // adjusting realtimeOffset.
                playbackBlockEnd = Moment.Max(now, playbackBlockEnd);
                playbackOffset = playbackBlockEnd - entryBeginsAt;
            }

            var playAt = entryBeginsAt + playbackOffset;
            playbackBlockEnd = Moment.Max(playbackBlockEnd, entryEndsAt + playbackOffset);

            var enqueueDelay = playAt - now - EnqueueAheadDuration;
            if (enqueueDelay > TimeSpan.Zero)
                await cpuClock.Delay(enqueueDelay, cancellationToken).ConfigureAwait(false);

            entryPlayer.EnqueueEntry(entry, skipTo, playAt);
        }
    }

    public Task<Moment?> GetRewindMoment(Moment playingAt, TimeSpan shift, CancellationToken cancellationToken)
    {
        if (shift == TimeSpan.Zero)
            return Task.FromResult<Moment?>(playingAt);
        if (shift.Ticks < 0)
            return GetRewindMomentInPast(playingAt, shift.Negate(), cancellationToken);
        return GetRewindMomentInFuture(playingAt, shift, cancellationToken);
    }

    private async Task<Moment?> GetRewindMomentInFuture(Moment playingAt, TimeSpan shift, CancellationToken cancellationToken)
    {
        if (shift <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(shift));
        var audioEntryReader = Chats.NewEntryReader(Session, ChatId, ChatEntryType.Audio);
        var idRange = await Chats.GetIdRange(Session, ChatId, ChatEntryType.Audio, cancellationToken)
            .ConfigureAwait(false);
        var startEntry = await audioEntryReader
            .FindByMinBeginsAt(playingAt - Constants.Chat.MaxEntryDuration, idRange, cancellationToken)
            .ConfigureAwait(false);
        if (startEntry == null) {
            Log.LogWarning("Couldn't find start entry");
            return null;
        }

        idRange = (startEntry.Id, idRange.End);
        var entries = audioEntryReader.Read(idRange, cancellationToken);
        var remainedShift = shift;
        var lastShiftPosition = playingAt;
        await foreach (var entry in entries.ConfigureAwait(false)) {
            if (!entry.StreamId.IsEmpty) // Streaming entry
                continue;
            if (entry.EndsAt < playingAt)
                // We're normally starting @ (playingAt - ChatConstants.MaxEntryDuration),
                // so we need to skip a few entries.
                continue;

            var entryBeginsAt = Moment.Max(entry.BeginsAt, lastShiftPosition);
            var entryEndsAt = entry.EndsAt ?? entry.BeginsAt + InfDuration;

            var expectedRewindPosition = entryBeginsAt + remainedShift;
            if (expectedRewindPosition <= entryEndsAt)
                return expectedRewindPosition;
            var shiftDuration = entryEndsAt - entryBeginsAt;
            remainedShift -= shiftDuration;
            lastShiftPosition = entryEndsAt;
        }
        return lastShiftPosition; // return max position that we reached
    }

    private async Task<Moment?> GetRewindMomentInPast(Moment playingAt, TimeSpan shift, CancellationToken cancellationToken)
    {
        if (shift <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(shift));
        var audioEntryReader = Chats.NewEntryReader(Session, ChatId, ChatEntryType.Audio);
        var idRange = await Chats.GetIdRange(Session, ChatId, ChatEntryType.Audio, cancellationToken)
            .ConfigureAwait(false);
        var startEntry = await audioEntryReader
            .FindByMinBeginsAt(playingAt - Constants.Chat.MaxEntryDuration, idRange, cancellationToken)
            .ConfigureAwait(false);
        if (startEntry == null) {
            Log.LogWarning("Couldn't find start entry");
            return null;
        }

        idRange = (startEntry.Id, idRange.End);
        var entries = audioEntryReader.Read(idRange, cancellationToken);
        ChatEntry? lastEntry = null;
        await foreach (var entry in entries.ConfigureAwait(false)) {
            if (!entry.StreamId.IsEmpty) // Streaming entry
                continue;
            if (entry.EndsAt >= playingAt) {
                // We're normally starting @ (playingAt - ChatConstants.MaxEntryDuration),
                // so we need to find an entry that completes after @ playingAt.
                lastEntry = entry;
                break;
            }
        }
        if (lastEntry == null) {
            Log.LogWarning("Couldn't find last entry");
            return null;
        }

        idRange = ((Range<long>)(idRange.Start, lastEntry.Id)).ToExclusive();
        var reverseEntries = audioEntryReader.ReadReverse(idRange, cancellationToken);
        var remainedShift = shift;
        var lastShiftPosition = playingAt;
        await foreach (var entry in reverseEntries.ConfigureAwait(false)) {
            if (!entry.StreamId.IsEmpty) // Streaming entry
                continue;
            if (entry.BeginsAt >= playingAt)
                // We're normally should not enter here due to way how last entry is looked up.
                continue;

            var entryBeginsAt = entry.BeginsAt;
            var entryEndsAt = entry.EndsAt.HasValue
                ? Moment.Min(entry.EndsAt.Value, lastShiftPosition)
                : lastShiftPosition;

            var expectedRewindPosition = entryEndsAt - remainedShift;
            if (expectedRewindPosition >= entryBeginsAt)
                return expectedRewindPosition;
            var shiftDuration = entryEndsAt - entryBeginsAt;
            remainedShift -= shiftDuration;
            lastShiftPosition = entryBeginsAt;
        }
        return lastShiftPosition; // return min position that we reached
    }
}
