using System.Collections.Concurrent;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;

namespace SosuBot.PerformanceCalculator;

/// <summary>
/// Shared cache for expensive parse, conversion and difficulty phases.
/// Random/seeded mods are deliberately excluded by the caller.
/// </summary>
internal static class PPCalculationCache
{
    private static readonly ConcurrentDictionary<string, WorkingBeatmap> WorkingBeatmaps = new();
    private static readonly ConcurrentDictionary<string, IBeatmap> PlayableBeatmaps = new();
    private static readonly ConcurrentDictionary<string, DifficultyAttributes> DifficultyAttributes = new();

    public static bool TryGetWorkingBeatmap(string key, out WorkingBeatmap beatmap) =>
        WorkingBeatmaps.TryGetValue(key, out beatmap!);

    public static void SetWorkingBeatmap(string key, WorkingBeatmap beatmap) =>
        WorkingBeatmaps[key] = beatmap;

    public static bool TryGetPlayableBeatmap(string key, out IBeatmap beatmap) =>
        PlayableBeatmaps.TryGetValue(key, out beatmap!);

    public static void SetPlayableBeatmap(string key, IBeatmap beatmap) =>
        PlayableBeatmaps[key] = beatmap;

    public static bool TryGetDifficultyAttributes(string key, out DifficultyAttributes attributes) =>
        DifficultyAttributes.TryGetValue(key, out attributes!);

    public static void SetDifficultyAttributes(string key, DifficultyAttributes attributes) =>
        DifficultyAttributes[key] = attributes;
}
