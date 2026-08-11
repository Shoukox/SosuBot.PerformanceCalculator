using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace SosuBot.PerformanceCalculator;

internal readonly record struct ProvidedHitStatistics(
    int? LargeTickMisses,
    int? SliderTailHits,
    int? Greats,
    int? Oks,
    int? Goods,
    int? Mehs);

internal static class PPCalculationStatistics
{
    public static int? GetHitObjectsLimit(PPCalculationRequest request)
    {
        if (request.Passed || request.ScoreStatistics is null)
            return null;

        return GetHitResultsCount(request.ScoreStatistics);
    }

    public static int GetHitResultsCount(Dictionary<HitResult, int> statistics)
    {
        return statistics.GetValueOrDefault(HitResult.Miss)
               + statistics.GetValueOrDefault(HitResult.Meh)
               + statistics.GetValueOrDefault(HitResult.Ok)
               + statistics.GetValueOrDefault(HitResult.Good)
               + statistics.GetValueOrDefault(HitResult.Great)
               + statistics.GetValueOrDefault(HitResult.Perfect);
    }

    public static ProvidedHitStatistics ReadProvided(Dictionary<HitResult, int>? statistics)
    {
        return new ProvidedHitStatistics(
            GetOptional(statistics, HitResult.LargeTickMiss),
            GetOptional(statistics, HitResult.SliderTailHit),
            GetOptional(statistics, HitResult.Great),
            GetOptional(statistics, HitResult.Ok),
            GetOptional(statistics, HitResult.Good),
            GetOptional(statistics, HitResult.Meh));
    }

    public static double ResolveAccuracy(
        int rulesetId,
        IBeatmap playableBeatmap,
        Mod[] mods,
        double? requestedAccuracy,
        Dictionary<HitResult, int>? scoreStatistics)
    {
        double accuracy = requestedAccuracy ?? CalculateAccuracy(
            rulesetId,
            playableBeatmap,
            mods,
            scoreStatistics ?? throw new ArgumentException("Score statistics are required."));

        return rulesetId == 1 ? Math.Clamp(accuracy, 0.5, 1) : accuracy;
    }

    public static Dictionary<HitResult, int> Generate(
        int rulesetId,
        IBeatmap playableBeatmap,
        Mod[] mods,
        double accuracy,
        Dictionary<HitResult, int>? originalStatistics,
        int sliderCount)
    {
        ProvidedHitStatistics provided = ReadProvided(originalStatistics);
        int misses = originalStatistics?.GetValueOrDefault(HitResult.Miss, 0) ?? 0;

        return rulesetId switch
        {
            0 => AccuracyTools.Osu.GenerateHitResults(
                playableBeatmap,
                mods,
                accuracy,
                provided.Oks,
                provided.Mehs,
                misses,
                provided.LargeTickMisses,
                provided.SliderTailHits ?? sliderCount),
            1 => AccuracyTools.Taiko.GenerateHitResults(
                playableBeatmap,
                mods,
                accuracy,
                misses,
                provided.Oks),
            2 => AccuracyTools.Catch.GenerateHitResults(
                playableBeatmap,
                mods,
                accuracy,
                misses,
                provided.Mehs,
                provided.Goods),
            3 => AccuracyTools.Mania.GenerateHitResults(
                playableBeatmap,
                mods,
                accuracy,
                provided.Greats,
                provided.Oks,
                provided.Goods,
                provided.Mehs,
                misses),
            _ => throw new ArgumentOutOfRangeException(nameof(rulesetId), rulesetId, "Unsupported ruleset.")
        };
    }

    public static double CalculateAccuracy(
        int rulesetId,
        IBeatmap playableBeatmap,
        Mod[] mods,
        Dictionary<HitResult, int> statistics)
    {
        return rulesetId switch
        {
            0 => AccuracyTools.Osu.GetAccuracy(playableBeatmap, statistics, mods),
            1 => AccuracyTools.Taiko.GetAccuracy(playableBeatmap, statistics, mods),
            2 => AccuracyTools.Catch.GetAccuracy(playableBeatmap, statistics, mods),
            3 => AccuracyTools.Mania.GetAccuracy(statistics, mods),
            _ => throw new ArgumentOutOfRangeException(nameof(rulesetId), rulesetId, "Unsupported ruleset.")
        };
    }

    private static int? GetOptional(Dictionary<HitResult, int>? statistics, HitResult result)
    {
        return statistics?.TryGetValue(result, out int value) == true ? value : null;
    }
}
