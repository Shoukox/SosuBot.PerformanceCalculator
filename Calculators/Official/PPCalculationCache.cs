using Microsoft.Extensions.Caching.Memory;

namespace SosuBot.PerformanceCalculator;

/// <summary>
/// Limits of the official calculator's in-memory cache.
/// </summary>
public sealed record PPCalculationCacheOptions
{
    public static PPCalculationCacheOptions Default { get; } = new();

    /// <summary>
    /// Maximum number of cached (beatmap content, ruleset, mods, object limit) combinations. An entry holds only
    /// difficulty attributes and object counts (about 1-2 KB), never the parsed beatmap, so the default stays
    /// within a few tens of megabytes.
    /// </summary>
    public int MaxEntries { get; init; } = 10_000;

    /// <summary>
    /// An entry that nobody asked for during this time is dropped even if the cache is not full.
    /// </summary>
    public TimeSpan SlidingExpiration { get; init; } = TimeSpan.FromHours(6);
}

/// <summary>
/// Bounded cache of the expensive, mod-dependent part of a calculation (conversion and difficulty).
/// Parsed and converted beatmaps are deliberately not cached: they weigh megabytes each and were the reason the
/// previous unbounded cache filled the memory.
/// </summary>
internal sealed class PPCalculationCache : IDisposable
{
    private readonly MemoryCache _entries;
    private readonly TimeSpan _slidingExpiration;

    public PPCalculationCache(PPCalculationCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxEntries);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.SlidingExpiration, TimeSpan.Zero);

        _slidingExpiration = options.SlidingExpiration;
        _entries = new MemoryCache(new MemoryCacheOptions
        {
            // Every entry has size 1, so the limit is an entry count; least recently used entries go first.
            SizeLimit = options.MaxEntries,
            CompactionPercentage = 0.1
        });
    }

    public int Count => _entries.Count;

    public bool TryGet(string key, out PPCalculationBeatmap beatmap) =>
        _entries.TryGetValue(key, out beatmap!);

    public void Set(string key, PPCalculationBeatmap beatmap) =>
        _entries.Set(key, beatmap, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = _slidingExpiration
        });

    public void Dispose() => _entries.Dispose();
}
