using System.Collections.Concurrent;

namespace SosuBot.PerformanceCalculator;

internal sealed class BeatmapsCacheDatabase
{
    private const string BEATMAP_DOWNLOAD_URL = "https://osu.ppy.sh/osu/";
    private const int CACHING_DAYS = 7;
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    private readonly ConcurrentDictionary<int, SemaphoreSlim> _syncDict = new();
    private readonly HttpClient httpClient = new();

    /// <summary>
    ///     Creates an instance of the class with a specified path to the cache directory.
    ///     Then creates the cache directory if needed
    /// </summary>
    /// <param name="cacheDirectory">If null, it uses the default directory (/cache) </param>
    public BeatmapsCacheDatabase(string? cacheDirectory = null)
    {
        CacheDirectory = cacheDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "beatmaps");
    }

    public string CacheDirectory { get; }

    public void CreateCacheDirectoryIfNeeded()
    {
        _semaphoreSlim.Wait();
        if (!Directory.Exists(CacheDirectory)) Directory.CreateDirectory(CacheDirectory);
        _semaphoreSlim.Release();
    }

    private string GetCachedBeatmapPath(int beatmapId)
    {
        return Path.Combine(CacheDirectory, $"{beatmapId}.osu");
    }

    private bool IsBeatmapCached(int beatmapId)
    {
        var filePath = GetCachedBeatmapPath(beatmapId);
        return File.Exists(filePath) &&
               !string.IsNullOrEmpty(File.ReadAllText(filePath).Trim());
    }

    private bool IsBeatmapCacheExpired(int beatmapId)
    {
        var lastModified = File.GetLastWriteTime(GetCachedBeatmapPath(beatmapId));
        return (DateTime.Now - lastModified).TotalDays > CACHING_DAYS;
    }

    public bool ShouldBeBeatmapCached(int beatmapId)
    {
        return !IsBeatmapCached(beatmapId) || IsBeatmapCacheExpired(beatmapId);
    }

    public byte[] GetCachedBeatmapContentAsByteArray(int beatmapId)
    {
        if (!IsBeatmapCached(beatmapId)) throw new FileNotFoundException();

        return File.ReadAllBytes(GetCachedBeatmapPath(beatmapId));
    }

    /// <summary>
    ///     Downloads and caches a given beatmap
    /// </summary>
    /// <param name="beatmapId"></param>
    /// <returns></returns>
    public async Task<byte[]> CacheBeatmap(int beatmapId)
    {
        CreateCacheDirectoryIfNeeded();

        var semaphoreSlim = _syncDict.GetOrAdd(beatmapId, new SemaphoreSlim(1, 1));
        await semaphoreSlim.WaitAsync();

        var response = await httpClient.GetAsync($"{BEATMAP_DOWNLOAD_URL}{beatmapId}");

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to download beatmap {beatmapId}. Status code: {response.StatusCode}");

        var contentAsByteArray = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(GetCachedBeatmapPath(beatmapId), contentAsByteArray);

        semaphoreSlim.Release();
        return contentAsByteArray;
    }
}