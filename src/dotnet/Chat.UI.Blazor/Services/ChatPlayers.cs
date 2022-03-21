﻿namespace ActualChat.Chat.UI.Blazor.Services;

/// <summary> Must be scoped service. </summary>
public class ChatPlayers : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Symbol, ChatPlayer> _players = new();
    private readonly IServiceProvider _services;
    private readonly ILogger<ChatPlayers> _log;
    private int _isDisposed;

    public ChatPlayers(IServiceProvider services, ILogger<ChatPlayers> log)
    {
        _services = services;
        _log = log;
    }

    [ComputeMethod]
    public virtual Task<ChatPlayer?> GetPlayer(Symbol chatId)
        => Task.FromResult(_players.GetValueOrDefault(chatId));

    public virtual ChatPlayer ActivatePlayer(Symbol chatId)
    {
        if (_isDisposed == 1)
            throw new ObjectDisposedException(nameof(ChatPlayers));
        var player = _players.GetOrAdd(chatId,
            static (key, self) => self._services.Activate<ChatPlayer>(key),
            this);

        using (Computed.Invalidate()) {
            _ = GetPlayer(chatId);
        }
        return player;
    }

    /// <summary> Disposes all resources allocated for <paramref name="chatId"/> </summary>
    public async ValueTask Close(Symbol chatId)
    {
        if (_isDisposed == 1)
            throw new ObjectDisposedException(nameof(ChatPlayers));
        if (_players.TryRemove(chatId, out var player)) {
            await player.DisposeAsync().ConfigureAwait(false);
            _log.LogDebug("Disposed player for chat #{ChatId}", chatId);
        }

        using (Computed.Invalidate()) {
            _ = GetPlayer(chatId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
            return;

        GC.SuppressFinalize(this);

        var players = _players.ToArray();
        _players.Clear();
        using (Computed.Invalidate()) {
            foreach (var player in players) {
                _ = GetPlayer(player.Key);
            }
        }

        var playerDisposeTasks = players
            .Select(kv => DisposePlayer(kv.Key, kv.Value))
            .ToArray();

        if (playerDisposeTasks.Length > 0)
            await Task.WhenAll(playerDisposeTasks).ConfigureAwait(false);

        async Task DisposePlayer(Symbol chatId, ChatPlayer player)
        {
            try {
                await player.DisposeAsync().ConfigureAwait(true);
                _log.LogDebug("Disposed player for chat #{ChatId}", chatId);
            }
            catch (Exception e) {
                _log.LogError(e, "Can't dispose player for chat #{ChatId}", chatId);
            }
        }
    }
}
