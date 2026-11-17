namespace SosuBot.PerformanceCalculator;

/// <summary>
/// Backwards-compatible facade for the official osu! ruleset performance
/// calculator. The calculation pipeline lives in <see cref="PPCalculationEngine"/>;
/// this class keeps the API used by the bot and external callers.
/// </summary>
public sealed class PPCalculator : IPerformanceCalculator, IDisposable
{
    private readonly PPCalculationEngine _engine;

    public PPCalculator()
        : this(PPCalculationCacheOptions.Default)
    {
    }

    public PPCalculator(PPCalculationCacheOptions cacheOptions)
        : this(new PPCalculationEngine(new PPCalculationCache(cacheOptions)))
    {
    }

    internal PPCalculator(PPCalculationEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>Number of prepared beatmaps currently cached.</summary>
    public int CachedBeatmapCount => _engine.Cache.Count;

    public Task<PPCalculationResult?> CalculatePpAsync(
        int beatmapId,
        Stream beatmapFile,
        double? accuracy = null,
        bool passed = true,
        int? scoreMaxCombo = null,
        osu.Game.Rulesets.Mods.Mod[]? scoreMods = null,
        Dictionary<osu.Game.Rulesets.Scoring.HitResult, int>? scoreStatistics = null,
        int rulesetId = 0,
        CancellationToken? cancellationToken = null,
        Dictionary<osu.Game.Rulesets.Scoring.HitResult, int>? maximumStatistics = null,
        long? legacyTotalScore = null)
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
            MaximumStatistics = maximumStatistics,
            LegacyTotalScore = legacyTotalScore,
            RulesetId = rulesetId,
            CancellationToken = cancellationToken
        });
    }

    public void Dispose() => _engine.Dispose();
}
