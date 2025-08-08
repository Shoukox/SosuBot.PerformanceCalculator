using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Scoring;
using osu.Game.Skinning;
using SosuBot.PerformanceCalculator.Models;

namespace SosuBot.PerformanceCalculator;

/// <summary>
///     One instance for the same beatmap with same mods in order to use the calculated data of it.
/// </summary>
public class PPCalculator
{
    /// <summary>
    ///     hitObjects is null, if it's the whole beatmap
    /// </summary>
    private static readonly ConcurrentDictionary<DifficultyAttributesKey, DifficultyAttributes>
        CachedDifficultyAttrbiutes = new();

    /// <summary>
    ///     hitObjects is null, if it's the whole beatmap
    /// </summary>
    private static readonly ConcurrentDictionary<DifficultyAttributesKey, WorkingBeatmap> CachedWorkingBeatmaps = new();

    /// <summary>
    ///     hitObjects is null, if it's the whole beatmap
    /// </summary>
    private static readonly ConcurrentDictionary<DifficultyAttributesKey, IBeatmap> CachedBeatmaps = new();

    private static readonly BeatmapsCacheDatabase BeatmapsCacheDatabase = new();

    public PPCalculator()
    {
        Logger.Level = LogLevel.Error;
    }

    public DifficultyAttributes LastDifficultyAttributes { get; set; }

    /// <summary>
    ///     Calculates pp for given ruleset
    /// </summary>
    /// <param name="beatmapId">Beatmap ID</param>
    /// <param name="accuracy">Accuracy (optional). If not given, scoreStatistics will be used</param>
    /// <param name="scoreMaxCombo">Score max combo. If null, then beatmap's maximum combo will be used</param>
    /// <param name="scoreMods">Score mods. If null, the no mods will be used (equals to lazer nomod score)</param>
    /// <param name="scoreStatistics">Score statistics. If null, then accuracy will be used to. For better results please pass score statistics from Score Model from API. Some values like IgnoreMiss come only from API.</param>
    /// <param name="scoreMaxStatistics">
    ///     For accuracy calculation. Score maximum statistics. Not necessarely should be equal to
    ///     scoreProcessor.MaximumStatistics. If null, then beatmap's maximum statistics will be used
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
    public async Task<double> CalculatePPAsync(
        int beatmapId,
        double? accuracy = null,
        int? scoreMaxCombo = null,
        Mod[]? scoreMods = null,
        Dictionary<HitResult, int>? scoreStatistics = null,
        int rulesetId = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
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
            byte[] beatmapBytes;
            if (BeatmapsCacheDatabase.ShouldBeBeatmapCached(beatmapId))
                beatmapBytes = await BeatmapsCacheDatabase.CacheBeatmap(beatmapId);
            else
                beatmapBytes = BeatmapsCacheDatabase.GetCachedBeatmapContentAsByteArray(beatmapId);

            int? hitObjects = null;
            if (scoreStatistics != null) hitObjects = GetHitObjectsCountForGivenStatistics(scoreStatistics);

            DifficultyAttributesKey key = new(beatmapId, hitObjects, scoreMods);
            if (!CachedWorkingBeatmaps.TryGetValue(key, out var workingBeatmap))
            {
                workingBeatmap = ParseBeatmap(beatmapBytes, hitObjects);
                CachedWorkingBeatmaps[key] = workingBeatmap;
            }

            if (!CachedBeatmaps.TryGetValue(key, out var playableBeatmap))
            {
                playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, scoreMods, cancellationToken);
                CachedBeatmaps[key] = playableBeatmap;
            }

            // Get score info
            if (scoreStatistics is null && accuracy is not null) // If FC, calculate only for acc
            {
                scoreStatistics = CalculateScoreStatistics(rulesetId, playableBeatmap, scoreMods, accuracy.Value);
            }
            else if (scoreStatistics is not null && accuracy is null)
            {
                accuracy = CalculateAccuracy(rulesetId, playableBeatmap, scoreMods, scoreStatistics);
            }
            else if (scoreStatistics is not null && accuracy is not null) // we have both accuracy and statistics from API. Adjust statistics using accuracy to save some API values like IgnoreMiss.
            {
                var calculatedScoreStatistics = CalculateScoreStatistics(rulesetId, playableBeatmap, scoreMods, accuracy.Value, 
                    scoreStatistics[HitResult.Miss], scoreStatistics[HitResult.Great], scoreStatistics[HitResult.Ok], 
                    scoreStatistics[HitResult.Good], scoreStatistics[HitResult.Meh]);
                
                calculatedScoreStatistics[HitResult.LargeTickMiss] = scoreStatistics[HitResult.LargeTickMiss];
                calculatedScoreStatistics[HitResult.SliderTailHit] = scoreStatistics[HitResult.SliderTailHit];
                calculatedScoreStatistics[HitResult.IgnoreMiss] = scoreStatistics[HitResult.IgnoreMiss];
                calculatedScoreStatistics[HitResult.IgnoreHit] = scoreStatistics[HitResult.IgnoreHit];
                foreach (var (k, p) in calculatedScoreStatistics)
                {
                    scoreStatistics[k] = p;
                }
            }

            scoreMaxCombo ??= playableBeatmap.GetMaxCombo();

            var scoreInfo = new ScoreInfo
            {
                Accuracy = accuracy.Value,
                Mods = scoreMods,
                MaxCombo = scoreMaxCombo.Value,
                Statistics = scoreStatistics,
                BeatmapInfo = playableBeatmap.BeatmapInfo
            };

            // Calculate pp
            var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
            if (!CachedDifficultyAttrbiutes.TryGetValue(key, out var difficultyAttributes))
                difficultyAttributes = difficultyCalculator.Calculate(scoreMods, cancellationToken);
            LastDifficultyAttributes = difficultyAttributes;
            var ppCalculator = ruleset.CreatePerformanceCalculator()!;
            var ppAttributes = await ppCalculator.CalculateAsync(scoreInfo, difficultyAttributes, cancellationToken);
            return ppAttributes.Total;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calculating PP: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            throw;
        }
    }

    private int GetHitObjectsCountForGivenStatistics(Dictionary<HitResult, int> statistics)
    {
        statistics.TryGetValue(HitResult.Miss, out var miss);
        statistics.TryGetValue(HitResult.Meh, out var meh);
        statistics.TryGetValue(HitResult.Ok, out var ok);
        statistics.TryGetValue(HitResult.Good, out var good);
        statistics.TryGetValue(HitResult.Great, out var great);
        statistics.TryGetValue(HitResult.Perfect, out var perfect);
        return miss + meh + ok + good + great + perfect;
    }

    private Dictionary<HitResult, int> CalculateScoreStatistics(int rulesetId, IBeatmap playableBeatmap, Mod[] scoreMods, double accuracy, int misses = 0, int? greatsMania = null, int? oksMania = null, int? goods = null, int? mehs = null)
    {
        return rulesetId switch
        {
            0 => AccuracyTools.Osu.GenerateHitResults(playableBeatmap, scoreMods, accuracy, goods, mehs, misses),
            1 => AccuracyTools.Taiko.GenerateHitResults(playableBeatmap, scoreMods, accuracy, misses, goods),
            2 => AccuracyTools.Catch.GenerateHitResults(playableBeatmap, scoreMods, accuracy, misses, mehs, goods),
            3 => AccuracyTools.Mania.GenerateHitResults(playableBeatmap, scoreMods, accuracy, greatsMania, oksMania, goods, mehs),
            _ => throw new NotImplementedException()
        };
    }

    private double CalculateAccuracy(int rulesetId, IBeatmap playableBeatmap, Mod[] scoreMods, Dictionary<HitResult, int> scoreStatistics)
    {
        return rulesetId switch
        {
            0 => AccuracyTools.Osu.GetAccuracy(playableBeatmap, scoreStatistics),
            1 => AccuracyTools.Taiko.GetAccuracy(playableBeatmap, scoreStatistics, scoreMods),
            2 => AccuracyTools.Catch.GetAccuracy(playableBeatmap, scoreStatistics, scoreMods),
            3 => AccuracyTools.Mania.GetAccuracy(playableBeatmap, scoreStatistics, scoreMods),
            _ => throw new NotImplementedException()
        };
    }

    private WorkingBeatmap ParseBeatmap(byte[] beatmapBytes, int? hitObjectsLimit = null)
    {
        try
        {
            // Create a working beatmap from the file
            using var stream = new MemoryStream(beatmapBytes);
            using var streamReader = new LineBufferedReader(stream);

            var versionText = UTF32Encoding.Default.GetString(beatmapBytes[..30]);
            var version = int.Parse(Regex.Match(versionText, @"v(?<ver>\d+)").Groups["ver"].Value);

            var decoder = new LegacyBeatmapDecoder(version);
            var beatmap = decoder.Decode(streamReader);

            if (hitObjectsLimit != null) beatmap.HitObjects = beatmap.HitObjects.Take(hitObjectsLimit.Value).ToList();

            return new LoadedBeatmap(beatmap);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing beatmap: {ex.Message}");
            throw;
        }
    }
}

// Simple implementation of WorkingBeatmap for the calculator
public class LoadedBeatmap : WorkingBeatmap
{
    private readonly IBeatmap beatmap;

    public LoadedBeatmap(IBeatmap beatmap) : base(beatmap.BeatmapInfo, null)
    {
        this.beatmap = beatmap;
    }

    public override Stream? GetStream(string storagePath)
    {
        return null;
    }

    public override Texture? GetBackground()
    {
        return null;
    }

    protected override IBeatmap GetBeatmap()
    {
        return beatmap;
    }

    protected override Track? GetBeatmapTrack()
    {
        return null;
    }

    protected override ISkin? GetSkin()
    {
        return null;
    }
}