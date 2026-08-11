namespace SosuBot.PerformanceCalculator;

/// <summary>
/// Backwards-compatible facade for the official osu! ruleset performance
/// calculator. The calculation pipeline lives in <see cref="PPCalculationEngine"/>;
/// this class keeps the API used by the bot and external callers.
/// </summary>
public class PPCalculator : IPerformanceCalculator
{
    private readonly PPCalculationEngine _engine;

    public PPCalculator()
        : this(new PPCalculationEngine())
    {
    }

    internal PPCalculator(PPCalculationEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public Task<PPCalculationResult?> CalculatePpAsync(
        int beatmapId,
        Stream beatmapFile,
        double? accuracy = null,
        bool passed = true,
        int? scoreMaxCombo = null,
        osu.Game.Rulesets.Mods.Mod[]? scoreMods = null,
        Dictionary<osu.Game.Rulesets.Scoring.HitResult, int>? scoreStatistics = null,
        int rulesetId = 0,
        CancellationToken? cancellationToken = null)
    {
        return _engine.CalculateAsync(new PPCalculationRequest
        {
            BeatmapId = beatmapId,
            BeatmapFile = beatmapFile,
            Accuracy = accuracy,
            Passed = passed,
            ScoreMaxCombo = scoreMaxCombo,
            ScoreMods = scoreMods ?? [],
            ScoreStatistics = scoreStatistics,
            RulesetId = rulesetId,
            CancellationToken = cancellationToken
        });
    }
}
