using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;

namespace SosuBot.PerformanceCalculator;

internal static class PPCalculationRulesetFactory
{
    public static Ruleset Create(int rulesetId) => rulesetId switch
    {
        0 => new OsuRuleset(),
        1 => new TaikoRuleset(),
        2 => new CatchRuleset(),
        3 => new ManiaRuleset(),
        _ => throw new ArgumentOutOfRangeException(nameof(rulesetId), rulesetId, "Unsupported ruleset.")
    };
}
