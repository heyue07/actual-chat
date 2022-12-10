namespace ActualChat.Collections;

public static class LruCacheExt
{
    public static TValue GetOrCreate<TKey, TValue>(
        this ILruCache<TKey, TValue> cache,
        TKey key,
        Func<TKey, TValue> factory)
        where TKey : notnull
    {
        if (cache.TryGetValue(key, out var value))
            return value;

        value = factory.Invoke(key);
        cache.TryAdd(key, value);
        return value;
    }
}
