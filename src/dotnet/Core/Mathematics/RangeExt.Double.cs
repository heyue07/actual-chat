namespace ActualChat.Mathematics;

public static class RangeExt
{
    public static Range<double> Expand(this Range<double> range, double startExpand, double endExpand)
        => new(range.Start - startExpand, range.End + endExpand);

    public static Range<double> FitInto(this Range<double> range, Range<double> fitRange)
    {
        var maxSize = Math.Min(range.Size(), fitRange.Size());
        return range.Resize(maxSize).ScrollInto(fitRange);
    }

    public static Range<double> ScrollInto(this Range<double> range, Range<double> fitRange)
    {
        var size = range.Size();
        if (range.End > fitRange.End)
            range = (fitRange.End - size, fitRange.End);
        if (range.Start < fitRange.Start)
            range = (fitRange.Start, size);
        return range;
    }
}
