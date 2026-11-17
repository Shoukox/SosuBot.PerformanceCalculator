using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace SosuBot.PerformanceCalculator;

public interface IPerformanceCalculator
{
    /// <param name="maximumStatistics">
    /// <c>maximum_statistics</c> of a submitted score. For a passed score with <paramref name="scoreStatistics" />
    /// it makes the result match the pp osu! servers award: statistics are taken as they are, not estimated.
    /// </param>
    /// <param name="legacyTotalScore"><c>legacy_total_score</c> of an osu!stable score.</param>
    Task<PPCalculationResult?> CalculatePpAsync(
        int beatmapId,
        Stream beatmapFile,
        double? accuracy = null,
        bool passed = true,
        int? scoreMaxCombo = null,
        Mod[]? scoreMods = null,
        Dictionary<HitResult, int>? scoreStatistics = null,
        int rulesetId = 0,
        CancellationToken? cancellationToken = null,
        Dictionary<HitResult, int>? maximumStatistics = null,
        long? legacyTotalScore = null);
}
