using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ActualChat.Streaming
{
    public interface IAudioStreamingService : IStreamingService<AudioMessage>
    {
        Task UploadRecording(AudioRecordingConfiguration audioConfig, ChannelReader<AudioMessage> source, CancellationToken cancellationToken);
    }
}