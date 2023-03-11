using System.Numerics;
using System.Text.RegularExpressions;

namespace ActualChat.Transcription;

[DataContract]
public sealed record Transcript(
    [property: DataMember(Order = 0)] string Text,
    [property: DataMember(Order = 1)] LinearMap TimeMap)
{
    public static readonly Regex ContentStartRe =
        new(@"^\s*", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
    public static readonly Regex ContentEndRe =
        new(@"\s*$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

    public static Vector2 TimeMapEpsilon { get; } = new(0.1f, 0.1f);
    public static Transcript Empty { get; } = new();

    [JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public int Length => Text.Length;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public Range<int> TextRange => new(0, Text.Length);
    [JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public Range<float> TimeRange => TimeMap.YRange;

    public Transcript()
        : this("", LinearMap.Zero) { }

    public override string ToString()
        => $"`{Text}` + {TimeMap}";

    public void RequireValid()
        => TimeMap.RequireValid();

    public int GetContentStart()
        => ContentStartRe.Match(Text).Length;

    public int GetContentEnd()
        => Length - ContentEndRe.Match(Text).Length;

    public float GetContentStartTime()
        => TimeMap.Map(GetContentStart());

    public float GetContentEndTime()
        => TimeMap.Map(GetContentEnd());

    public Transcript GetPrefix(int length, float? duration = null)
    {
        var vDuration = duration ?? TimeMap.Map(length);
        return new (Text[..length], TimeMap.GetPrefix(vDuration, TimeMapEpsilon.X));
    }

    public Transcript GetSuffix(int start, float? startDuration = null)
    {
        var vStartDuration = startDuration ?? TimeMap.Map(start);
        return new (Text[start..], TimeMap.GetSuffix(vStartDuration, TimeMapEpsilon.X));
    }

    public (Transcript Prefix, Transcript Suffix) Split(int length, float? duration = null)
    {
        var vDuration = duration ?? TimeMap.Map(length);
        var maps = TimeMap.Split(vDuration);
        var prefix = new Transcript(Text[..length], maps.Prefix);
        var suffix = new Transcript(Text[length..], maps.Suffix);
        return (prefix, suffix);
    }

    public Transcript WithSuffix(string suffix, float? suffixEndTime = null)
    {
        var text = Text + suffix;
        var timeMap = TimeMap;
        if (suffixEndTime is { } vSuffixEndTime)
            timeMap = timeMap.TryAppend(new Vector2(text.Length, vSuffixEndTime), TimeMapEpsilon.X);
        return new Transcript(text, timeMap);
    }

    public Transcript WithSuffix(string suffix, LinearMap suffixTextToTimeMap)
    {
        var text = Text + suffix;
        var timeMap = TimeMap.AppendOrUpdateSuffix(suffixTextToTimeMap, TimeMapEpsilon.X);
        return new Transcript(text, timeMap);
    }

    // Operators

    public static TranscriptDiff operator -(Transcript transcript, Transcript baseTranscript)
        => TranscriptDiff.New(transcript, baseTranscript);
}
