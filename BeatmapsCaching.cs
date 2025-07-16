using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SosuBot.PerformanceCalculator
{
    internal sealed class BeatmapsCaching
    {
        #region Singleton
        public static BeatmapsCaching Instance
        {
            get
            {
                return _instance.Value;
            }
        }
        private static Lazy<BeatmapsCaching> _instance = new Lazy<BeatmapsCaching>(() => new BeatmapsCaching(), true);
        #endregion

        private readonly HttpClient httpClient;
        public const string BEATMAP_DOWNLOAD_URL = "https://osu.ppy.sh/osu/";
        public const int CACHING_DAYS = 7;
        public readonly string CacheDirectory;

        private ConcurrentDictionary<int, SemaphoreSlim> _syncDict = new ConcurrentDictionary<int, SemaphoreSlim>();
        private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Creates an instance of the class with a specified path to the cache directory.
        /// Then creates the cache directory if needed
        /// </summary>
        /// <param name="cacheDirectory">If null, it uses the default directory (/cache) </param>
        private BeatmapsCaching(string? cacheDirectory = null)
        {
            httpClient = new HttpClient();
            CacheDirectory = cacheDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
        }

        public void CreateCacheDirectoryIfNeeded()
        {
            _semaphoreSlim.Wait();
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
            _semaphoreSlim.Release();
        }

        private string GetCachedBeatmapPath(int beatmapId)
        {
            return Path.Combine(CacheDirectory, $"{beatmapId}.osu");
        }

        private bool IsBeatmapCached(int beatmapId) => File.Exists(GetCachedBeatmapPath(beatmapId));
        private bool IsBeatmapCacheExpired(int beatmapId)
        {
            DateTime lastModified = File.GetLastWriteTime(GetCachedBeatmapPath(beatmapId));
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
        /// Downloads and cached the given beatmap
        /// </summary>
        /// <param name="beatmapId"></param>
        /// <returns></returns>
        public async Task<byte[]> CacheBeatmap(int beatmapId)
        {
            var semaphoreSlim = _syncDict.GetOrAdd(beatmapId, new SemaphoreSlim(1, 1));
            await semaphoreSlim.WaitAsync();

            var response = await httpClient.GetAsync($"{BEATMAP_DOWNLOAD_URL}{beatmapId}");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to download beatmap {beatmapId}. Status code: {response.StatusCode}");

            byte[] contentAsByteArray = await response.Content.ReadAsByteArrayAsync();
            File.WriteAllBytes(GetCachedBeatmapPath(beatmapId), contentAsByteArray);

            semaphoreSlim.Release();
            return contentAsByteArray;
        }
    }
}
