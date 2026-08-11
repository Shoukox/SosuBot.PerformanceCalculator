using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace SosuBot.PerformanceCalculator;

public interface IPerformanceCalculator
{
    Task<PPCalculationResult?> CalculatePpAsync(
        int beatmapId,
        Stream beatmapFile,
        double? accuracy = null,
        bool passed = true,
        int? scoreMaxCombo = null,
        Mod[]? scoreMods = null,
        Dictionary<HitResult, int>? scoreStatistics = null,
        int rulesetId = 0,
        CancellationToken? cancellationToken = null);
}
