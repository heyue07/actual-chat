namespace ActualChat.Audio.UI.Blazor.Components;

public interface IAudioPlayerBackend
{
    Task OnPlaying(double offset, bool isPaused, bool isBufferLow);
    Task OnEnded(string? errorMessage);
}
