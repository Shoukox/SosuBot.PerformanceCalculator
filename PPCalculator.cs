using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Scoring;
using SosuBot.PerformanceCalculator.Models;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

#pragma warning disable CS0618 // Type or member is obsolete

namespace SosuBot.PerformanceCalculator;

/// <summary>
///     One instance for the same beatmap with same mods for pp calculation.
/// </summary>
// ReSharper disable once InconsistentNaming
public class PPCalculator
{
    /// <summary>
    ///     todo
    /// </summary>
    private static readonly ConcurrentDictionary<DifficultyAttributesKey, DifficultyAttributes>
        CachedDifficultyAttrbiutes = new();

    /// <summary>
    ///     todo
    /// </summary>
    private static readonly ConcurrentDictionary<DifficultyAttributesKey, WorkingBeatmap> CachedWorkingBeatmaps = new();

    /// <summary>
    ///     todo
    /// </summary>
    private static readonly ConcurrentDictionary<DifficultyAttributesKey, IBeatmap> CachedBeatmaps = new();

    /// <summary>
    ///     Whether to cache the values
    /// </summary>
    private readonly bool _cache = true;

    /// <summary>
    ///     Creates an instance of <see cref="PPCalculator" />
    /// </summary>
    /// <param name="logger">
    ///     Used logger. If not provided, a default instance from <see cref="LoggerFactory" /> with console
    ///     logging and logging_level=debug will be used
    /// </param>
    public PPCalculator(ILogger<PPCalculator>? logger = null)
    {
        // Setup default logger if needed
        if (logger == null)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options => options.SingleLine = true);
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            Logger = loggerFactory.CreateLogger<PPCalculator>();
        }
        else
        {
            Logger = logger;
        }

        BeatmapTools.Initialize(Logger);
    }

    /// <summary>
    ///     Logger
    /// </summary>
    private ILogger<PPCalculator> Logger { get; set; }

    public DifficultyAttributes? LastDifficultyAttributes { get; private set; }

    /// <summary>
    ///     Calculates pp for given ruleset
    /// </summary>
    /// <param name="beatmapId">Beatmap ID</param>
    /// <param name="accuracy">Accuracy. If not given, scoreStatistics will be used</param>
    /// <param name="passed">Is the score passed</param>
    /// <param name="scoreMaxCombo">Score max combo. If null, then beatmap's maximum combo will be used</param>
    /// <param name="scoreMods">Score mods. If null, the no mods will be used (equals to lazer nomod score)</param>
    /// <param name="scoreStatistics">
    ///     Score statistics.
    ///     If null, then the calculation will be for a FC with given accuracy
    /// </param>
    /// <param name="rulesetId">
    ///     The play mode for pp calculation.
    ///     Std = 0,
    ///     Taiko = 1,
    ///     Catch = 2,
    ///     Mania = 3
    /// </param>
    /// <param name="cancellationToken">Cancellation token with cancellation support</param>
    /// <returns>Total pp</returns>
    public async Task<PPCalculationResult> CalculatePpAsync(
        int beatmapId,
        double? accuracy = null,
        bool passed = true,
        int? scoreMaxCombo = null,
        Mod[]? scoreMods = null,
        Dictionary<HitResult, int>? scoreStatistics = null,
        int rulesetId = 0,
        CancellationToken? cancellationToken = null)
    {
        var hashCode = GetHashCode();
        try
        {
            if (cancellationToken == null)
            {
                CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
                cancellationToken = cts.Token;
            }

            scoreMods ??= [];

            Ruleset ruleset = rulesetId switch
            {
                0 => new OsuRuleset(),
                1 => new TaikoRuleset(),
                2 => new CatchRuleset(),
                3 => new ManiaRuleset(),
                _ => new OsuRuleset()
            };

            // Download beatmap
            byte[] beatmapBytes = await BeatmapTools.GetBeatmapBytes(beatmapId);
            if (beatmapBytes.Length <= 30)
            {
                Logger.LogWarning($"Beatmap bytes length: {beatmapBytes.Length}");
                if (beatmapBytes.Length != 0)
                {
                    Logger.LogWarning($"Beatmap bytes content as string: {Encoding.Default.GetString(beatmapBytes)}");
                }

                throw new TimeoutException("Failed to cache a beatmap");
            }

            int? hitObjectsLimit = null;
            
            // impossible case. scoreStatistics null means fc, but a fc can't be not passed.
            if (scoreStatistics == null && !passed)
                throw new Exception("Impossible case: scoreStatistics = null and passed = false");

            // if scoreStatistics is null, then it's full combo
            // scoreStatistics not null and not passed mean the scoreStatistics contains not all hitobjects of the map
            // hitObjects is not null only if the score was not passed
            if (scoreStatistics != null && !passed)
                hitObjectsLimit = GetHitResultsCountForGivenStatistics(scoreStatistics);

            DifficultyAttributesKey key = new(beatmapId, hitObjectsLimit, scoreMods.OrderBy(m => m.Acronym).ToArray());

            Logger.LogInformation($"[{hashCode}] bool cache = {_cache}");
            Logger.LogInformation(
                $"[{hashCode}] Current key: {key.BeatmapId} {key.HitObjects} {JsonConvert.SerializeObject(key.Mods)}");

            if (!CachedWorkingBeatmaps.TryGetValue(key, out var workingBeatmap))
            {
                workingBeatmap = ParseBeatmap(beatmapBytes, hitObjectsLimit);
                Logger.LogInformation($"[{hashCode}] Parsed beatmap");

                if (_cache && !scoreMods.Any(m => m is OsuModRandom))
                {
                    CachedWorkingBeatmaps[key] = workingBeatmap;
                    Logger.LogInformation($"[{hashCode}] Cache the parsed beatmap");
                }
            }

            if (!CachedBeatmaps.TryGetValue(key, out var playableBeatmap))
            {
                playableBeatmap =
                    workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, scoreMods, cancellationToken.Value);
                Logger.LogInformation($"[{hashCode}] Parsed a working beatmap => playable beatmap");

                if (_cache && !scoreMods.Any(m => m is ModRandom))
                {
                    CachedBeatmaps[key] = playableBeatmap;
                    Logger.LogInformation($"[{hashCode}] Cache the playable beatmap");
                }
            }

            var beatmapSliderTails = playableBeatmap.HitObjects.Count(x => x is Slider);
            int? largeTickMisses = null, sliderTailHits = null, greats = null, oks = null, goods = null, mehs = null;
            if (scoreStatistics != null)
            {
                if (scoreStatistics.TryGetValue(HitResult.LargeTickMiss, out var largeTickMissesFromDict))
                    largeTickMisses = largeTickMissesFromDict;
                if (scoreStatistics.TryGetValue(HitResult.SliderTailHit, out var sliderTailHitsFromDict))
                    sliderTailHits = sliderTailHitsFromDict;
                if (scoreStatistics.TryGetValue(HitResult.Great, out var greatsFromDict))
                    greats = greatsFromDict;
                if (scoreStatistics.TryGetValue(HitResult.Ok, out var oksFromDict))
                    oks = oksFromDict;
                if (scoreStatistics.TryGetValue(HitResult.Good, out var goodsFromDict))
                    goods = goodsFromDict;
                if (scoreStatistics.TryGetValue(HitResult.Meh, out var mehsFromDict))
                    mehs = mehsFromDict;
            }
            scoreStatistics = CalculateScoreStatistics(rulesetId, playableBeatmap, scoreMods, accuracy!.Value,
                scoreStatistics?.GetValueOrDefault(HitResult.Miss, 0) ?? 0,
                largeTickMisses,
                sliderTailHits ?? beatmapSliderTails,
                greats,
                oks,
                goods,
                mehs
            );

            var scoreStatisticsAccuracy = CalculateAccuracy(rulesetId, playableBeatmap, scoreMods, scoreStatistics);
            int beatmapHitObjectsCount = playableBeatmap.HitObjects.Count;
            int beatmapMaxCombo = playableBeatmap.GetMaxCombo();
            scoreMaxCombo ??= beatmapMaxCombo;
            var scoreInfo = new ScoreInfo(playableBeatmap.BeatmapInfo, ruleset.RulesetInfo)
            {
                Accuracy = scoreStatisticsAccuracy,
                Mods = scoreMods,
                MaxCombo = scoreMaxCombo.Value,
                Statistics = scoreStatistics
            };

            // Calculate pp
            var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
            if (!CachedDifficultyAttrbiutes.TryGetValue(key, out var difficultyAttributes))
            {
                difficultyAttributes = difficultyCalculator.Calculate(scoreMods, cancellationToken.Value);
                Logger.LogInformation($"[{hashCode}] Calculated difficulty attributes");

                if (_cache && !scoreMods.Any(m => m is ModRandom))
                {
                    CachedDifficultyAttrbiutes[key] = difficultyAttributes;
                    Logger.LogInformation($"[{hashCode}] Cached the difficulty attributes");
                }
            }

            LastDifficultyAttributes = difficultyAttributes;
            var ppCalculator = ruleset.CreatePerformanceCalculator()!;
            var ppAttributes =
                await ppCalculator.CalculateAsync(scoreInfo, difficultyAttributes, cancellationToken.Value);

            Logger.LogInformation($"[{hashCode}] Calculated total pp: {ppAttributes.Total}");

            return new PPCalculationResult
            {
                Pp = ppAttributes.Total,
                CalculatedAccuracy = scoreStatisticsAccuracy,
                DifficultyAttributes = difficultyAttributes,
                BeatmapMaxCombo = beatmapMaxCombo,
                BeatmapHitObjectsCount = beatmapHitObjectsCount,
                ScoreHitResultsCount = GetHitResultsCountForGivenStatistics(scoreStatistics)
            };
        }
        catch (Exception ex)
        {
            Logger.LogInformation($"[{hashCode}] Error calculating PP: {ex.Message}");
            if (ex.InnerException != null)
                Logger.LogInformation($"[{hashCode}] Inner exception: {ex.InnerException.Message}");
            throw;
        }
    }

    private int GetHitResultsCountForGivenStatistics(Dictionary<HitResult, int> statistics)
    {
        statistics.TryGetValue(HitResult.Miss, out var miss);
        statistics.TryGetValue(HitResult.Meh, out var meh);
        statistics.TryGetValue(HitResult.Ok, out var ok);
        statistics.TryGetValue(HitResult.Good, out var good);
        statistics.TryGetValue(HitResult.Great, out var great);
        statistics.TryGetValue(HitResult.Perfect, out var perfect);
        return miss + meh + ok + good + great + perfect;
    }

    private Dictionary<HitResult, int> CalculateScoreStatistics(int rulesetId, IBeatmap playableBeatmap,
        Mod[] scoreMods, double accuracy, int misses = 0, int? largeTickMisses = null, int? sliderTailHits = null,
        int? greats = null, int? oks = null,
        int? goods = null, int? mehs = null)
    {
        return rulesetId switch
        {
            0 => AccuracyTools.Osu.GenerateHitResults(playableBeatmap, scoreMods, accuracy, oks, mehs, misses,
                largeTickMisses, sliderTailHits),
            1 => AccuracyTools.Taiko.GenerateHitResults(playableBeatmap, scoreMods, accuracy, misses, goods),
            2 => AccuracyTools.Catch.GenerateHitResults(playableBeatmap, scoreMods, accuracy, misses, mehs, goods),
            3 => AccuracyTools.Mania.GenerateHitResults(playableBeatmap, scoreMods, accuracy, greats, oks,
                goods, mehs, misses),
            _ => throw new NotImplementedException()
        };
    }

    private double CalculateAccuracy(int rulesetId, IBeatmap playableBeatmap, Mod[] scoreMods,
        Dictionary<HitResult, int> scoreStatistics)
    {
        return rulesetId switch
        {
            0 => AccuracyTools.Osu.GetAccuracy(playableBeatmap, scoreStatistics, scoreMods),
            1 => AccuracyTools.Taiko.GetAccuracy(playableBeatmap, scoreStatistics, scoreMods),
            2 => AccuracyTools.Catch.GetAccuracy(playableBeatmap, scoreStatistics, scoreMods),
            3 => AccuracyTools.Mania.GetAccuracy(scoreStatistics, scoreMods),
            _ => throw new NotImplementedException()
        };
    }

    private WorkingBeatmap ParseBeatmap(byte[] beatmapBytes, int? hitObjectsLimit = null)
    {
        try
        {
            return BeatmapTools.ParseBeatmap(beatmapBytes, hitObjectsLimit);
        }
        catch (Exception ex)
        {
            Logger.LogInformation($"Error parsing beatmap: {ex.Message}");
            throw;
        }
    }
}