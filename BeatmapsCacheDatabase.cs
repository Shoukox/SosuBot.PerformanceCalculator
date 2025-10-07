using System.Collections.Concurrent;

namespace SosuBot.PerformanceCalculator;

internal sealed class BeatmapsCacheDatabase
{
    private const string BeatmapDownloadUrl = "https://osu.ppy.sh/osu/";
    private const int CachingDays = 7;
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    private readonly ConcurrentDictionary<int, SemaphoreSlim> _syncDict = new();
    private readonly HttpClient _httpClient = new(){ Timeout = TimeSpan.FromSeconds(10) };

    private readonly CancellationToken _cts;

    /// <summary>
    ///     Creates an instance of the class with a specified path to the cache directory.
    ///     Then creates the cache directory if needed
    /// </summary>
    /// <param name="cts">Cancellation token</param>
    /// <param name="cacheDirectory">If null, it uses the default directory (/cache) </param>
    public BeatmapsCacheDatabase(CancellationToken cts, string? cacheDirectory = null)
    {
        CacheDirectory = cacheDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "beatmaps");
        _cts = cts;
    }

    public string CacheDirectory { get; }

    public void CreateCacheDirectoryIfNeeded()
    {
        _semaphoreSlim.Wait(_cts);
        try
        {
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    private string GetCachedBeatmapPath(int beatmapId)
    {
        return Path.Combine(CacheDirectory, $"{beatmapId}.osu");
    }

    private bool IsBeatmapCached(int beatmapId)
    {
        var filePath = GetCachedBeatmapPath(beatmapId);
        var fileInfo = new FileInfo(filePath);
        return File.Exists(filePath) &&
               fileInfo.Length > 30;
    }

    private bool IsBeatmapCacheExpired(int beatmapId)
    {
        var lastModified = File.GetLastWriteTime(GetCachedBeatmapPath(beatmapId));
        return (DateTime.Now - lastModified).TotalDays > CachingDays;
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
        await semaphoreSlim.WaitAsync(_cts);

        try
        {
            if (IsBeatmapCached(beatmapId) && !IsBeatmapCacheExpired(beatmapId))
            {
                return GetCachedBeatmapContentAsByteArray(beatmapId);
            }

            using var response = await _httpClient.GetAsync($"{BeatmapDownloadUrl}{beatmapId}",
                HttpCompletionOption.ResponseContentRead, _cts);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to download beatmap {beatmapId}. Status code: {response.StatusCode}");

            var contentAsByteArray = await response.Content.ReadAsByteArrayAsync(_cts);
            if (contentAsByteArray == null || contentAsByteArray.Length <= 30)
                throw new Exception($"Downloaded beatmap {beatmapId} is empty or too small (size: {contentAsByteArray?.Length ?? 0} bytes)");
            
            await File.WriteAllBytesAsync(GetCachedBeatmapPath(beatmapId), contentAsByteArray, _cts);
            return contentAsByteArray;
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
}