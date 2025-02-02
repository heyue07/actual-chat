using Microsoft.Toolkit.HighPerformance.Buffers;

namespace ActualChat.Kvas;

public static class KvasExt
{
    public static IByteSerializer Serializer { get; set; } = KvasSerializer.Default;
    public static readonly string MigratedKey = "@Migrated";

    // Get, Set, Remove w/ <T>

    public static async ValueTask<Option<T>> TryGet<T>(this IKvas kvas, string key, CancellationToken cancellationToken = default)
  {
        var data = await kvas.Get(key, cancellationToken).ConfigureAwait(false);
        return data is null ? Option<T>.None : Serializer.Read<T>(data);
    }

    public static ValueTask<T?> Get<T>(this IKvas kvas, string key, CancellationToken cancellationToken = default)
        => Get<T>(kvas, key, default, cancellationToken);

    public static async ValueTask<T?> Get<T>(this IKvas kvas, string key, T? @default, CancellationToken cancellationToken = default)
    {
        var (hasValue, value) = await TryGet<T>(kvas, key, cancellationToken).ConfigureAwait(false);
        return hasValue ? value ?? @default : @default;
    }

    public static Task Set<T>(this IKvas kvas, string key, T value, CancellationToken cancellationToken = default)
    {
        using var buffer = new ArrayPoolBufferWriter<byte>();
        Serializer.Write(buffer, value);
        return kvas.Set(key, buffer.WrittenMemory.ToArray(), cancellationToken);
    }

    public static Task Set<T>(this IKvas kvas, string key, Option<T> value, CancellationToken cancellationToken = default)
        => value.IsSome(out var v)
            ? kvas.Set(key, v, cancellationToken)
            : kvas.Remove(key, cancellationToken);

    public static Task Remove(this IKvas kvas, string key, CancellationToken cancellationToken = default)
        => kvas.Set(key, null, cancellationToken);

    public static async Task WhenMigrated(this IKvas kvas, CancellationToken cancellationToken = default)
    {
        // NOTE(AY): This method is unused for now, but I decided to keep it - just in case
        var cMigratedTask = Computed.Capture(() => kvas.Get(MigratedKey, cancellationToken));
        var cSettingsTask = Computed.Capture(() => kvas.Get("UserLanguageSettings", cancellationToken));
        var cMigrated = await cMigratedTask.ConfigureAwait(false);
        var cSettings = await cSettingsTask.ConfigureAwait(false);
        var task1 = cMigrated.When(x => x != null, cancellationToken);
        var task2 = cSettings.When(x => x != null, cancellationToken);
        await Task.WhenAny(task1, task2).ConfigureAwait(false);
    }

    // WithXxx

    public static IKvas WithPrefix<T>(this IKvas kvas)
        => kvas.WithPrefix(typeof(T));

    public static IKvas WithPrefix(this IKvas kvas, Type type)
        => kvas.WithPrefix(type.GetName());

    public static IKvas WithPrefix(this IKvas kvas, string prefix)
    {
        if (prefix.IsNullOrEmpty())
            return kvas;
        if (kvas is PrefixedKvas kvp)
            return new PrefixedKvas(kvp.Upstream, $"{prefix}.{kvp.Prefix}");
        return new PrefixedKvas(kvas, prefix);
    }
}
