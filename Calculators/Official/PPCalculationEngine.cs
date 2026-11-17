using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace SosuBot.PerformanceCalculator;

internal sealed class PPCalculationEngine(PPCalculationCache cache) : IDisposable
{
    internal PPCalculationCache Cache { get; } = cache ?? throw new ArgumentNullException(nameof(cache));

    public async Task<PPCalculationResult?> CalculateAsync(PPCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using CancellationTokenSource? timeoutSource = request.CancellationToken is null
            ? new CancellationTokenSource(TimeSpan.FromSeconds(30))
            : null;
        CancellationToken cancellationToken = timeoutSource?.Token ?? request.CancellationToken!.Value;

        try
        {
            if (request.ScoreStatistics is null && !request.Passed)
                throw new ArgumentException("A failed calculation requires score statistics.");

            if (request.Accuracy is null && request.ScoreStatistics is null)
                throw new ArgumentException("Accuracy or score statistics must be provided.");

            Mod[] mods = request.ScoreMods;
            Ruleset ruleset = PPCalculationRulesetFactory.Create(request.RulesetId);
            int? hitObjectsLimit = request.IsSubmittedScore ? null : PPCalculationStatistics.GetHitObjectsLimit(request);
            PPCalculationBeatmap map = GetBeatmap(request, ruleset, mods, hitObjectsLimit, cancellationToken);

            Dictionary<HitResult, int> scoreStatistics;
            double calculatedAccuracy;
            if (request.IsSubmittedScore)
            {
                // What osu-queue-score-statistics feeds the calculator: the stored statistics and the accuracy
                // osu! derives from them.
                scoreStatistics = new Dictionary<HitResult, int>(request.ScoreStatistics!);
                calculatedAccuracy = StandardisedScoreMigrationTools.ComputeAccuracy(
                    scoreStatistics,
                    request.MaximumStatistics!,
                    ruleset.CreateScoreProcessor());
            }
            else
            {
                double accuracy = PPCalculationStatistics.ResolveAccuracy(
                    request.RulesetId,
                    map.Counts,
                    mods,
                    request.Accuracy,
                    request.ScoreStatistics);
                scoreStatistics = PPCalculationStatistics.Generate(
                    request.RulesetId,
                    map.Counts,
                    mods,
                    accuracy,
                    request.ScoreStatistics);
                calculatedAccuracy = PPCalculationStatistics.CalculateAccuracy(
                    request.RulesetId,
                    map.Counts,
                    mods,
                    scoreStatistics);
            }

            int scoreMaxCombo = request.ScoreMaxCombo ?? map.Counts.MaxCombo;

            var scoreInfo = new ScoreInfo(map.BeatmapInfo, ruleset.RulesetInfo)
            {
                Accuracy = calculatedAccuracy,
                Mods = mods,
                MaxCombo = scoreMaxCombo,
                Statistics = scoreStatistics,
                MaximumStatistics = request.MaximumStatistics is null
                    ? []
                    : new Dictionary<HitResult, int>(request.MaximumStatistics),
                LegacyTotalScore = request.LegacyTotalScore
            };

            osu.Game.Rulesets.Difficulty.PerformanceCalculator performanceCalculator =
                ruleset.CreatePerformanceCalculator()
                ?? throw new InvalidOperationException($"No performance calculator for ruleset {request.RulesetId}.");
            PerformanceAttributes performanceAttributes = await performanceCalculator.CalculateAsync(
                scoreInfo,
                map.DifficultyAttributes,
                cancellationToken);

            return new PPCalculationResult
            {
                PP = performanceAttributes.Total,
                CalculatedAccuracy = calculatedAccuracy,
                DifficultyAttributes = map.DifficultyAttributes,
                PerformanceAttributes = performanceAttributes,
                BeatmapMaxCombo = map.Counts.MaxCombo,
                BeatmapHitObjectsCount = map.Counts.HitObjects,
                ScoreHitResultsCount = PPCalculationStatistics.GetHitResultsCount(scoreStatistics),
                CS = map.CircleSize,
                HP = map.DrainRate,
                OD = map.OverallDifficulty,
                AR = map.ApproachRate,
                SpeedChangeFactor = map.SpeedChangeFactor,
                ScoreStatistics = scoreStatistics
            };
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => Cache.Dispose();

    private PPCalculationBeatmap GetBeatmap(
        PPCalculationRequest request,
        Ruleset ruleset,
        Mod[] mods,
        int? hitObjectsLimit,
        CancellationToken cancellationToken)
    {
        // Randomising mods produce a different beatmap on every run unless they carry a seed.
        bool cacheable = !mods.Any(mod => mod is ModRandom or IHasSeed);
        string? key = cacheable
            ? PPCalculationCacheKey.Create(
                BeatmapFileParser.ComputeHash(request.BeatmapFile),
                request.RulesetId,
                hitObjectsLimit,
                mods)
            : null;

        if (key is not null && Cache.TryGet(key, out PPCalculationBeatmap cached))
            return cached;

        Beatmap decoded = BeatmapFileParser.Decode(request.BeatmapFile, hitObjectsLimit);
        PPCalculationBeatmap prepared = PPCalculationBeatmapLoader.Load(decoded, ruleset, mods, cancellationToken);

        if (key is not null)
            Cache.Set(key, prepared);

        return prepared;
    }
}
