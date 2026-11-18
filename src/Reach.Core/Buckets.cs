namespace Reach;

/// <summary>
/// The get-or-add every multi-valued index here is built with.
/// </summary>
internal static class Buckets
{
    /// <summary>
    /// The bucket for <paramref name="key"/>, created empty on first use.
    /// </summary>
    internal static TBucket Bucket<TKey, TBucket>(Dictionary<TKey, TBucket> index, TKey key)
        where TKey : notnull
        where TBucket : new()
    {
        if (!index.TryGetValue(key, out var bucket))
        {
            index[key] = bucket = new TBucket();
        }

        return bucket;
    }
}
