using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;

namespace SosuBot.PerformanceCalculator;

public static class BeatmapTools
{
    private static ILogger Logger = null!;
    private static BeatmapsCacheDatabase BeatmapsCacheDatabase = null!;

    public static void Initialize(ILogger logger)
    {
        Logger = logger;
        BeatmapsCacheDatabase = new(CancellationToken.None, logger);
    }
    public static LoadedBeatmap ParseBeatmap(byte[] beatmapBytes, int? hitObjectsLimit = null)
    {
        // Create a working beatmap from the file
        using var stream = new MemoryStream(beatmapBytes);
        using var streamReader = new LineBufferedReader(stream);

        var versionText = Encoding.Default.GetString(beatmapBytes[..30]);
        var version = int.Parse(Regex.Match(versionText, @"v(?<ver>\d+)").Groups["ver"].Value);

        var decoder = new LegacyBeatmapDecoder(version);
        var beatmap = decoder.Decode(streamReader);

        if (hitObjectsLimit != null) beatmap.HitObjects = beatmap.HitObjects.Take(hitObjectsLimit.Value).ToList();

        return new LoadedBeatmap(beatmap);
    }
    
    public static async Task<LoadedBeatmap> ParseBeatmap(int beatmapId, int? hitObjectsLimit = null)
    {
        byte[] beatmapBytes = await GetBeatmapBytes(beatmapId);
        return ParseBeatmap(beatmapBytes, hitObjectsLimit);
    }

    internal static async Task<byte[]> GetBeatmapBytes(int beatmapId)
    {
        byte[] beatmapBytes = [];
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                beatmapBytes = await BeatmapsCacheDatabase.CacheBeatmap(beatmapId);
                if (beatmapBytes.Length <= 30)
                {
                    Logger.LogWarning($"[Retry] Beatmap bytes length: {beatmapBytes.Length}");
                    if (beatmapBytes.Length != 0)
                    {
                        Logger.LogWarning($"[Retry] Beatmap bytes content as string: {Encoding.Default.GetString(beatmapBytes)}");
                    }
                    Logger.LogWarning($"[Retry] Retrying the {attempt} time to cache a beatmap...");
                    await Task.Delay(3000);
                    beatmapBytes = await BeatmapsCacheDatabase.CacheBeatmap(beatmapId);
                }

                break;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error from loop in ppCalc");
                await Task.Delay(1000);
            }
        }

        return beatmapBytes;
    }
}