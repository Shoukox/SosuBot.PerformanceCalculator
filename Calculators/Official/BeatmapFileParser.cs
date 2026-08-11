using System.Text;
using System.Text.RegularExpressions;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;

namespace SosuBot.PerformanceCalculator;

internal static partial class BeatmapFileParser
{
    public static WorkingBeatmap Parse(Stream beatmapFile, int? hitObjectsLimit = null)
    {
        ArgumentNullException.ThrowIfNull(beatmapFile);
        if (!beatmapFile.CanSeek)
            throw new ArgumentException("The beatmap stream must be seekable.", nameof(beatmapFile));

        using var streamReader = new LineBufferedReader(beatmapFile, true);
        beatmapFile.Position = 0;

        byte[] header = new byte[30];
        beatmapFile.ReadExactly(header);
        string versionText = Encoding.Default.GetString(header);
        Match versionMatch = VersionPattern().Match(versionText);
        if (!versionMatch.Success || !int.TryParse(versionMatch.Groups["version"].Value, out int version))
            throw new FormatException("The beatmap version could not be read.");

        Beatmap beatmap = new LegacyBeatmapDecoder(version).Decode(streamReader);
        if (hitObjectsLimit is { } limit)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(hitObjectsLimit));

            beatmap.HitObjects = beatmap.HitObjects.Take(limit).ToList();
        }

        return new LoadedBeatmap(beatmap);
    }

    [GeneratedRegex(@"v(?<version>\d+)")]
    private static partial Regex VersionPattern();
}
