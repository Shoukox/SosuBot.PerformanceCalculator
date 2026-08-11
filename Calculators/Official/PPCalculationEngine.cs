using osu.Game.Rulesets;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace SosuBot.PerformanceCalculator;

internal sealed class PPCalculationEngine
{
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
            int? hitObjectsLimit = PPCalculationStatistics.GetHitObjectsLimit(request);
            string cacheKey = PPCalculationCacheKey.Create(
                request.BeatmapId,
                request.RulesetId,
                hitObjectsLimit,
                mods);
            PPCalculationMap map = PPCalculationMapLoader.Load(
                request,
                ruleset,
                mods,
                hitObjectsLimit,
                cacheKey,
                cancellationToken);

            double accuracy = PPCalculationStatistics.ResolveAccuracy(
                request.RulesetId,
                map.PlayableBeatmap,
                mods,
                request.Accuracy,
                request.ScoreStatistics);
            Dictionary<HitResult, int> scoreStatistics = PPCalculationStatistics.Generate(
                request.RulesetId,
                map.PlayableBeatmap,
                mods,
                accuracy,
                request.ScoreStatistics,
                map.SliderCount);
            double calculatedAccuracy = PPCalculationStatistics.CalculateAccuracy(
                request.RulesetId,
                map.PlayableBeatmap,
                mods,
                scoreStatistics);
            int scoreMaxCombo = request.ScoreMaxCombo ?? map.MaxCombo;

            var scoreInfo = new ScoreInfo(map.PlayableBeatmap.BeatmapInfo, ruleset.RulesetInfo)
            {
                Accuracy = calculatedAccuracy,
                Mods = mods,
                MaxCombo = scoreMaxCombo,
                Statistics = scoreStatistics
            };

            DifficultyAttributes difficultyAttributes = GetDifficultyAttributes(
                cacheKey,
                map.WorkingBeatmap,
                ruleset,
                mods,
                cancellationToken);
            osu.Game.Rulesets.Difficulty.PerformanceCalculator performanceCalculator =
                ruleset.CreatePerformanceCalculator()
                ?? throw new InvalidOperationException($"No performance calculator for ruleset {request.RulesetId}.");
            PerformanceAttributes performanceAttributes = await performanceCalculator.CalculateAsync(
                scoreInfo,
                difficultyAttributes,
                cancellationToken);

            return new PPCalculationResult
            {
                PP = performanceAttributes.Total,
                CalculatedAccuracy = calculatedAccuracy,
                DifficultyAttributes = difficultyAttributes,
                BeatmapMaxCombo = map.MaxCombo,
                BeatmapHitObjectsCount = map.HitObjectsCount,
                ScoreHitResultsCount = PPCalculationStatistics.GetHitResultsCount(scoreStatistics),
                CS = map.CircleSize,
                HP = map.DrainRate,
                OD = map.OverallDifficulty,
                AR = map.ApproachRate,
                SpeedChangeFactor = map.SpeedChangeFactor
            };
        }
        catch
        {
            return null;
        }
    }

    private static DifficultyAttributes GetDifficultyAttributes(
        string cacheKey,
        WorkingBeatmap workingBeatmap,
        Ruleset ruleset,
        Mod[] mods,
        CancellationToken cancellationToken)
    {
        bool cacheDifficulty = !mods.Any(mod => mod is ModRandom);
        if (cacheDifficulty && PPCalculationCache.TryGetDifficultyAttributes(cacheKey, out DifficultyAttributes attributes))
            return attributes;

        DifficultyAttributes calculated = ruleset
            .CreateDifficultyCalculator(workingBeatmap)
            .Calculate(mods, cancellationToken);
        if (cacheDifficulty)
            PPCalculationCache.SetDifficultyAttributes(cacheKey, calculated);

        return calculated;
    }
}
