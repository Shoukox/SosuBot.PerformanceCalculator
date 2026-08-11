using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace SosuBot.PerformanceCalculator;

internal sealed record PPCalculationMap(
    WorkingBeatmap WorkingBeatmap,
    IBeatmap PlayableBeatmap,
    int HitObjectsCount,
    int MaxCombo,
    int SliderCount,
    double CircleSize,
    double DrainRate,
    double OverallDifficulty,
    double ApproachRate,
    double SpeedChangeFactor);

internal static class PPCalculationMapLoader
{
    public static PPCalculationMap Load(
        PPCalculationRequest request,
        Ruleset ruleset,
        Mod[] mods,
        int? hitObjectsLimit,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        bool cacheWorkingBeatmap = !mods.Any(mod => mod is IHasSeed);
        bool cachePlayableBeatmap = !mods.Any(mod => mod is ModRandom);

        WorkingBeatmap workingBeatmap;
        if (!cacheWorkingBeatmap || !PPCalculationCache.TryGetWorkingBeatmap(cacheKey, out workingBeatmap!))
        {
            workingBeatmap = BeatmapFileParser.Parse(request.BeatmapFile, hitObjectsLimit);
            if (cacheWorkingBeatmap)
                PPCalculationCache.SetWorkingBeatmap(cacheKey, workingBeatmap);
        }

        IBeatmap playableBeatmap;
        if (!cachePlayableBeatmap || !PPCalculationCache.TryGetPlayableBeatmap(cacheKey, out playableBeatmap!))
        {
            playableBeatmap = workingBeatmap.GetPlayableBeatmap(
                ruleset.RulesetInfo,
                mods,
                cancellationToken);
            if (cachePlayableBeatmap)
                PPCalculationCache.SetPlayableBeatmap(cacheKey, playableBeatmap);
        }

        double speedChangeFactor = mods.FirstOrDefault(mod => mod is ModRateAdjust) is ModRateAdjust rateMod
            ? rateMod.SpeedChange.Value
            : 1;

        return new PPCalculationMap(
            workingBeatmap,
            playableBeatmap,
            playableBeatmap.HitObjects.Count,
            playableBeatmap.GetMaxCombo(),
            playableBeatmap.HitObjects.Count(obj => obj is Slider),
            playableBeatmap.Difficulty.CircleSize,
            playableBeatmap.Difficulty.DrainRate,
            playableBeatmap.Difficulty.OverallDifficulty,
            playableBeatmap.Difficulty.ApproachRate,
            speedChangeFactor);
    }
}
