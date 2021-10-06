using ActualChat.Channels;
using ActualChat.Media;

namespace ActualChat.Audio;

public class AudioSource : MediaSource<AudioFormat, AudioFrame>
{
    public AudioSource(AudioFormat format, Task<TimeSpan> durationTask, AsyncMemoizer<AudioFrame> framesMemoizer)
        : base(format, durationTask, framesMemoizer) { }
}
