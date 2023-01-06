using Stl.IO;

namespace ActualChat.Audio.UnitTests;

public class ActualOpusStreamAdapterTests
{
    private readonly ILogger _log;

    public ActualOpusStreamAdapterTests(ILogger log)
        => _log = log;

    [Fact]
    public async Task ReadAndWrittenStreamIsTheSame()
    {
        var webMStreamAdapter = new WebMStreamAdapter(_log);
        var streamAdapter = new ActualOpusStreamAdapter(_log);
        var byteStream = GetAudioFilePath((FilePath)"0000-LONG.webm")
            .ReadByteStream(1024);
        var audio = await webMStreamAdapter.Read(byteStream, default);
        var outByteStreamMemoized = streamAdapter.Write(audio, CancellationToken.None).Memoize();

        var audio1 = await streamAdapter.Read(outByteStreamMemoized.Replay(), CancellationToken.None);
        var outByteStream1 = streamAdapter.Write(audio1, CancellationToken.None);

        var inList = await outByteStreamMemoized.Replay().ToListAsync();
        var outList = await outByteStream1.ToListAsync();
        var inArray = inList.SelectMany(chunk => chunk).ToArray();
        var outArray = outList.SelectMany(chunk => chunk).ToArray();
        inArray.Length.Should().Be(outArray.Length);
        inArray.Should().BeEquivalentTo(outArray);
    }

    [Fact]
    public async Task OneByteSequenceCanBeRead()
    {
        var webMStreamAdapter = new WebMStreamAdapter(_log);
        var streamAdapter = new ActualOpusStreamAdapter(_log);
        var byteStream = GetAudioFilePath((FilePath)"0000-LONG.webm")
            .ReadByteStream(1);
        var audio = await webMStreamAdapter.Read(byteStream, default);
        var outByteStreamMemoized = streamAdapter.Write(audio, CancellationToken.None).Memoize();

        var audio1 = await streamAdapter.Read(outByteStreamMemoized.Replay(), CancellationToken.None);
        var outByteStream1 = streamAdapter.Write(audio1, CancellationToken.None);

        var inList = await outByteStreamMemoized.Replay().ToListAsync();
        var outList = await outByteStream1.ToListAsync();
        var inArray = inList.SelectMany(chunk => chunk).ToArray();
        var outArray = outList.SelectMany(chunk => chunk).ToArray();
        inArray.Length.Should().Be(outArray.Length);
        inArray.Should().BeEquivalentTo(outArray);
    }

    [Fact]
    public async Task SuccessfulReadFromFile()
    {
        var streamAdapter = new ActualOpusStreamAdapter(_log);
        var byteStream = GetAudioFilePath((FilePath)"0000.opuss")
            .ReadByteStream( 1024);
        var audio = await streamAdapter.Read(byteStream, default);
        var outByteStream = streamAdapter.Write(audio, CancellationToken.None);
        var outList = await outByteStream.ToListAsync();
        var outArray = outList.SelectMany(chunk => chunk).ToArray();
        outArray.Length.Should().Be(10563); // we added preSkip with this commit
    }

    [Fact(Skip = "Manual")]
    public async Task ReadWriteFile()
    {
        var streamAdapter = new ActualOpusStreamAdapter(_log);
        var byteStream = GetAudioFilePath((FilePath)"silence.opuss")
            .ReadByteStream( 1024);
        await using var outputStream = new FileStream(
            Path.Combine(Environment.CurrentDirectory, "data", "silence-prefix.opuss"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite);
        var audio = await streamAdapter.Read(byteStream, default);
        var outByteStream = streamAdapter.Write(audio, CancellationToken.None).Take(101);
        var i = 0;
        await foreach (var x in outByteStream) {
            _log.LogInformation("{I}", i++);
            await outputStream.WriteAsync(x);
        }
        outputStream.Flush();
    }

    private static FilePath GetAudioFilePath(FilePath fileName)
        => new FilePath(Environment.CurrentDirectory) & "data" & fileName;
}
