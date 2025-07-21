using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Mods;
using OsuApi.Core.V2.Models;
using Mod = osu.Game.Rulesets.Mods.Mod;

namespace SosuBot.PerformanceCalculator;

public static class OsuTypesHelper
{
    public static Mod GetClassicMode(int playmode)
    {
        return playmode switch
        {
            0 => new OsuModClassic(),
            1 => new TaikoModClassic(),
            2 => new CatchModClassic(),
            3 => new ManiaModClassic(),
            _ => throw new NotImplementedException()
        };
    }

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