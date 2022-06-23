using ActualChat.Audio.UI.Blazor.Components;
using ActualChat.UI.Blazor.Services;

namespace ActualChat.Chat.UI.Blazor.Services;

public class ChatUIStatePersister : StatePersister<ChatUIStatePersister.Model>
{
    // TODO: optimize WASM so it would restore chat id faster
    private static TimeSpan ChatActivationTimeout { get; } = TimeSpan.FromSeconds(BlazorModeHelper.IsServerSideBlazor ? 1 : 3);

    private readonly Session _session;
    private readonly IChats _chats;
    private readonly ChatPlayers _chatPlayers;
    private readonly AudioRecorder _audioRecorder;
    private readonly ChatUI _chatUI;
    private readonly UserInteractionUI _userInteractionUI;

    public ChatUIStatePersister(
        Session session,
        IChats chats,
        ChatPlayers chatPlayers,
        AudioRecorder audioRecorder,
        ChatUI chatUI,
        UserInteractionUI userInteractionUI,
        IServiceProvider services)
        : base(services)
    {
        _session = session;
        _chats = chats;
        _chatPlayers = chatPlayers;
        _audioRecorder = audioRecorder;
        _chatUI = chatUI;
        _userInteractionUI = userInteractionUI;
    }

    protected override Task Restore(Model? state, CancellationToken cancellationToken)
    {
        if (state == null)
            return Task.CompletedTask;

        // We'll be waiting for chat activation, so let's do the rest as background task
        _ = BackgroundTask.Run(async () => {
            var pinnedChatIds = await Normalize(state.PinnedChatIds).ConfigureAwait(false);
            _chatUI.PinnedChatIds.Value = pinnedChatIds.ToImmutableHashSet();

            // Let's wait for activation of the last active chat before any further actions
            using var timoutCts = new CancellationTokenSource(ChatActivationTimeout);
            try {
                await _chatUI.ActiveChatId
                    .When(chatId => chatId == state.ActiveChatId, timoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException exc) {
                Log.LogDebug(exc, "Failed to restore ActiveChatId due to timeout");
            }

            var activeChatId = _chatUI.ActiveChatId.Value;
            if (activeChatId.IsEmpty || activeChatId != state.ActiveChatId)
                state = state with { IsPlayingActive = false };

            // Let's try to activate recording first
            if (state.IsPlayingActive || state.IsPlayingPinned) {
                var rules = await _chats.GetRules(_session, activeChatId, cancellationToken).ConfigureAwait(false);
                if (rules.CanRead) {
                    await _userInteractionUI.RequestInteraction("audio playback").ConfigureAwait(false);
                    _chatUI.IsPlaying.Value = state.IsPlayingActive;
                    _chatUI.MustPlayPinnedChats.Value = state.IsPlayingPinned;
                }
            }
        }, Log, "Error while restoring ChatPageState");
        return Task.CompletedTask;
    }

    protected override async Task<Model> Compute(CancellationToken cancellationToken)
    {
        var activeChatId = await _chatUI.ActiveChatId.Use(cancellationToken).ConfigureAwait(false);
        var pinnedChatIds = await _chatUI.PinnedChatIds.Use(cancellationToken).ConfigureAwait(false);
        var isPlayingActive = await _chatUI.IsPlaying.Use(cancellationToken).ConfigureAwait(false);
        var isPlayingPinned = await _chatUI.MustPlayPinnedChats.Use(cancellationToken).ConfigureAwait(false);
        return new Model() {
            ActiveChatId = activeChatId,
            PinnedChatIds = pinnedChatIds.ToArray(),
            IsPlayingActive = isPlayingActive,
            IsPlayingPinned = isPlayingPinned,
        };
    }

    private async Task<Symbol[]> Normalize(Symbol[] chatIds)
    {
        var rulesTasks = chatIds.Select(async chatId => {
            var rules = await _chats.GetRules(_session, chatId, default).ConfigureAwait(false);
            return (chatId, rules);
        });

        var rulesTuples = await Task.WhenAll(rulesTasks).ConfigureAwait(false);
        var result = new List<Symbol>();
        foreach (var (chatId, permissions) in rulesTuples) {
            if (!permissions.CanRead)
                continue;
            result.Add(chatId);
        }
        return result.ToArray();
    }

    public sealed record Model
    {
        public Symbol ActiveChatId { get; init; }
        public Symbol[] PinnedChatIds { get; init; } = Array.Empty<Symbol>();
        public bool IsPlayingActive { get; init; }
        public bool IsPlayingPinned { get; init; }
    }
}
