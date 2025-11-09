using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SosuBot.PerformanceCalculator;

internal sealed class BeatmapsCacheDatabase
{
    private const string BeatmapDownloadUrl = "https://osu.ppy.sh/osu/";
    private const int CachingDays = 7;

    private readonly ConcurrentDictionary<int, SemaphoreSlim> _syncDict = new();
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly CancellationToken _cts;
    private readonly ILogger _logger;

    /// <summary>
    ///     Creates an instance of the class with a specified path to the cache directory.
    ///     Then creates the cache directory if needed
    /// </summary>
    /// <param name="cts">Cancellation token</param>
    /// <param name="logger">A logger to be used</param>
    /// <param name="cacheDirectory">If null, it uses the default directory (/cache) </param>
    public BeatmapsCacheDatabase(CancellationToken cts, ILogger? logger = null, string? cacheDirectory = null)
    {
        CacheDirectory = cacheDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "beatmaps");
        _cts = cts;
        
        // Setup default logger if needed
        if (logger == null)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options => options.SingleLine = true);
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            _logger = loggerFactory.CreateLogger(nameof(BeatmapsCacheDatabase));
        }
        else
        {
            _logger = logger;
        }
        CreateCacheDirectory();
    }

    private string CacheDirectory { get; }

    private void CreateCacheDirectory()
    {
        Directory.CreateDirectory(CacheDirectory);
    }

    private string GetCachedBeatmapPath(int beatmapId)
    {
        return Path.Combine(CacheDirectory, $"{beatmapId}.osu");
    }

    private bool IsBeatmapCached(int beatmapId)
    {
        var filePath = GetCachedBeatmapPath(beatmapId);
        if (!File.Exists(filePath)) return false;
        
        return new FileInfo(filePath).Length > 30;
    }

    private bool IsBeatmapCacheExpired(int beatmapId)
    {
        var lastModified = File.GetLastWriteTime(GetCachedBeatmapPath(beatmapId));
        return (DateTime.Now - lastModified).TotalDays > CachingDays;
    }

    private byte[] GetCachedBeatmapContentAsByteArray(int beatmapId)
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
        CreateCacheDirectory();

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
            
            
            if (contentAsByteArray is not { Length: > 30 })
                throw new Exception(
                    $"Downloaded beatmap {beatmapId} is empty or too small (size: {contentAsByteArray?.Length ?? 0} bytes)");

            await File.WriteAllBytesAsync(GetCachedBeatmapPath(beatmapId), contentAsByteArray, _cts);
            return contentAsByteArray;
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
}