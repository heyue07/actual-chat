using ActualChat.Audio;
using ActualChat.Audio.UI.Blazor.Components;
using ActualChat.UI.Blazor.Services;
using Stl.Interception;

namespace ActualChat.Chat.UI.Blazor.Services;

public partial class ChatAudioUI : WorkerBase, IComputeService, INotifyInitialized
{
    private readonly IMutableState<Moment?> _stopRecordingAt;
    private readonly IMutableState<Moment?> _audioStoppedAt;
    private readonly TaskCompletionSource<Unit> _whenEnabledSource = TaskCompletionSourceExt.New<Unit>();
    private AudioSettings? _audioSettings;
    private AudioRecorder? _audioRecorder;
    private ChatPlayers? _chatPlayers;
    private IChats? _chats;
    private ActiveChatsUI? _activeChatsUI;
    private TuneUI? _tuneUI;
    private LanguageUI? _languageUI;
    private InteractiveUI? _interactiveUI;
    private DeviceAwakeUI? _deviceAwakeUI;
    private ChatEditorUI? _chatEditorUI;
    private UICommander? _uiCommander;

    private IServiceProvider Services { get; }
    private ILogger Log { get; }
    private ILogger? DebugLog => Constants.DebugMode.ChatUI ? Log : null;

    private Session Session { get; }
    private AudioSettings AudioSettings => _audioSettings ??= Services.GetRequiredService<AudioSettings>();
    private AudioRecorder AudioRecorder => _audioRecorder ??= Services.GetRequiredService<AudioRecorder>();
    private ChatPlayers ChatPlayers => _chatPlayers ??= Services.GetRequiredService<ChatPlayers>();
    private IChats Chats => _chats ??= Services.GetRequiredService<IChats>();
    private ActiveChatsUI ActiveChatsUI => _activeChatsUI ??= Services.GetRequiredService<ActiveChatsUI>();
    private TuneUI TuneUI => _tuneUI ??= Services.GetRequiredService<TuneUI>();
    private LanguageUI LanguageUI => _languageUI ??= Services.GetRequiredService<LanguageUI>();
    private InteractiveUI InteractiveUI => _interactiveUI ??= Services.GetRequiredService<InteractiveUI>();
    private DeviceAwakeUI DeviceAwakeUI => _deviceAwakeUI ??= Services.GetRequiredService<DeviceAwakeUI>();
    private ChatEditorUI ChatEditorUI => _chatEditorUI ??= Services.GetRequiredService<ChatEditorUI>();
    private UICommander UICommander => _uiCommander ??= Services.UICommander();
    private MomentClockSet Clocks { get; }

    private Moment Now => Clocks.SystemClock.Now;
    public IState<Moment?> StopRecordingAt => _stopRecordingAt;
    public Task<Unit> WhenEnabled => _whenEnabledSource.Task;
    public IState<Moment?> AudioStoppedAt => _audioStoppedAt;

    public ChatAudioUI(IServiceProvider services)
    {
        Services = services;
        Log = services.LogFor(GetType());

        Session = services.GetRequiredService<Session>();
        Clocks = services.Clocks();

        // Read entry states from other windows / devices are delayed by 1s
        var stateFactory = services.StateFactory();
        _stopRecordingAt = stateFactory.NewMutable<Moment?>();
        _audioStoppedAt = stateFactory.NewMutable<Moment?>();
    }

    void INotifyInitialized.Initialized()
        => this.Start();

    // ChatAudioUI is disabled until the moment user visits ChatPage
    public void Enable()
        => _whenEnabledSource.TrySetResult(default);

    [ComputeMethod] // Synced
    public virtual Task<ChatAudioState> GetState(ChatId chatId)
    {
        if (chatId.IsNone)
            return Task.FromResult(ChatAudioState.None);

        var activeChats = ActiveChatsUI.ActiveChats.Value;
        activeChats.TryGetValue(chatId, out var activeChat);
        var isListening = activeChat.IsListening;
        var isRecording = activeChat.IsRecording;
        var isPlayingHistorical = ChatPlayers.PlaybackState.Value is HistoricalPlaybackState hps && hps.ChatId == chatId;
        var result = new ChatAudioState(chatId, isListening, isPlayingHistorical, isRecording);
        return Task.FromResult(result);
    }

    [ComputeMethod] // Synced
    public virtual Task<ImmutableHashSet<ChatId>> GetListeningChatIds()
        => Task.FromResult(ActiveChatsUI.ActiveChats.Value.Where(c => c.IsListening).Select(c => c.ChatId).ToImmutableHashSet());

    public ValueTask SetListeningState(ChatId chatId, bool mustListen)
    {
        if (chatId.IsNone)
            return ValueTask.CompletedTask;

        var now = Now;
        return ActiveChatsUI.UpdateActiveChats(activeChats => {
            var oldActiveChats = activeChats;
            if (activeChats.TryGetValue(chatId, out var chat) && chat.IsListening != mustListen) {
                chat = chat with {
                    IsListening = mustListen,
                    ListeningRecency = mustListen ? now : chat.ListeningRecency,
                };
                activeChats = activeChats.AddOrUpdate(chat);
            }
            else if (mustListen)
                activeChats = activeChats.Add(new ActiveChat(chatId, true, false, now, now));
            if (oldActiveChats != activeChats)
                _ = UICommander.RunNothing();

            return activeChats;
        });
    }

    public ValueTask ClearListeningChats()
        => ActiveChatsUI.UpdateActiveChats(activeChats => {
            var oldActiveChats = activeChats;
            foreach (var chat in oldActiveChats) {
                if (chat.IsListening)
                    activeChats = activeChats.AddOrUpdate(chat with { IsListening = false });
            }
            if (oldActiveChats != activeChats)
                _ = UICommander.RunNothing();

            return activeChats;
        });

    [ComputeMethod] // Synced
    public virtual Task<ChatId> GetRecordingChatId()
        => Task.FromResult(ActiveChatsUI.ActiveChats.Value.FirstOrDefault(c => c.IsRecording).ChatId);

    public ValueTask SetRecordingChatId(ChatId chatId)
        => ActiveChatsUI.UpdateActiveChats(activeChats => {
            var oldChat = activeChats.FirstOrDefault(c => c.IsRecording);
            if (oldChat.ChatId == chatId)
                return activeChats;

            if (!oldChat.ChatId.IsNone)
                activeChats = activeChats.AddOrUpdate(oldChat with {
                    IsRecording = false,
                    Recency = Now,
                });
            if (!chatId.IsNone) {
                var newChat = new ActiveChat(chatId, true, true, Now);
                activeChats = activeChats.AddOrUpdate(newChat);
                _ = TuneUI.Play("begin-recording");
            }
            else
                _ = TuneUI.Play("end-recording");

            _ = UICommander.RunNothing();
            return activeChats;
        });

    [ComputeMethod] // Synced
    public virtual Task<bool> IsAudioOn()
        => Task.FromResult(ActiveChatsUI.ActiveChats.Value.Any(c => c.IsRecording || c.IsListening));

    [ComputeMethod]
    public virtual async Task<RealtimePlaybackState?> GetExpectedRealtimePlaybackState()
    {
        var listeningChatIds = await GetListeningChatIds().ConfigureAwait(false);
        return listeningChatIds.Count == 0 ? null : new RealtimePlaybackState(listeningChatIds);
    }
}
