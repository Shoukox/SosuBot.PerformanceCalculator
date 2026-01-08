using osu.Game.Rulesets.Scoring;
using OsuApi.V2.Models;

namespace SosuBot.PerformanceCalculator;

public static class OsuTypesHelper
{
    public static Dictionary<HitResult, int> ToStatistics(this ScoreStatistics statistics)
    {
        var result = new Dictionary<HitResult, int>();
        result.TryAdd(HitResult.Miss, statistics.Miss);
        result.TryAdd(HitResult.Meh, statistics.Meh);
        result.TryAdd(HitResult.Ok, statistics.Ok);
        result.TryAdd(HitResult.Good, statistics.Good);
        result.TryAdd(HitResult.Great, statistics.Great);
        result.TryAdd(HitResult.Perfect, statistics.Perfect);
        result.TryAdd(HitResult.SmallTickMiss, statistics.SmallTickMiss);
        result.TryAdd(HitResult.SmallTickHit, statistics.SmallTickHit);
        result.TryAdd(HitResult.LargeTickMiss, statistics.LargeTickMiss);
        result.TryAdd(HitResult.LargeTickHit, statistics.LargeTickHit);
        result.TryAdd(HitResult.SmallBonus, statistics.SmallBonus);
        result.TryAdd(HitResult.LargeBonus, statistics.LargeBonus);
        result.TryAdd(HitResult.IgnoreMiss, statistics.IgnoreMiss);
        result.TryAdd(HitResult.IgnoreHit, statistics.IgnoreHit);
        result.TryAdd(HitResult.SliderTailHit, statistics.SliderTailHit);
        return result;
    }
}