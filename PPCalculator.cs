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
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace SosuBot.PerformanceCalculator
{
    /*
     * 1. TimedDifficultyAttributes instead of CalculateTimed()
     * 2. Caching
     */

    /// <summary>
    /// One instance for the same beatmap with same mods in order to use the calculated data of it.
    /// </summary>
    public class PPCalculator
    {
        private static readonly ConcurrentDictionary<int, DifficultyAttributes> CachedDifficultyAttrbiutes = new();

        private WorkingBeatmap? _currentWorkingBeatmap;
        private IBeatmap? _currentPlayableBeatmap;
        private ScoreProcessor? _currentScoreProcessor;

        public PPCalculator()
        {
            Logger.Level = LogLevel.Error;
        }

        /// <summary>
        /// Calculates pp for given ruleset
        /// </summary>
        /// <param name="beatmapId">Beatmap ID</param>
        /// <param name="accuracy">Accuracy (optional). If not given, scoreStatistics will be used</param>
        /// <param name="scoreMaxCombo">Score max combo. If null, then beatmap's maximum combo will be used</param>
        /// <param name="scoreMods">Score mods. If null, the no mods will be used (equals to lazer nomod score)</param>
        /// <param name="scoreStatistics">Score statistics. If null, then accuracy will be used</param>
        /// <param name="scoreMaxStatistics">For accuracy calculation. Score maximum statistics. Not necessarely should be equal to scoreProcessor.MaximumStatistics. If null, then beatmap's maximum statistics will be used</param>
        /// <param name="rulesetId">
        ///         The play mode for pp calculation.
        ///         Std = 0,
        ///         Taiko = 1,
        ///         Catch = 2,
        ///         Mania = 3
        /// </param>
        /// <param name="cancellationToken">Cancellation token with cancellation support</param>
        /// <returns>Total pp</returns>
        public async Task<double> CalculatePPAsync(
            int beatmapId,
            double? accuracy = null,
            int? scoreMaxCombo = null,
            Mod[]? scoreMods = null,
            Dictionary<HitResult, int>? scoreStatistics = null,
            Dictionary<HitResult, int>? scoreMaxStatistics = null,
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
                if (BeatmapsCaching.Instance.ShouldBeBeatmapCached(beatmapId))
                {
                    beatmapBytes = await BeatmapsCaching.Instance.CacheBeatmap(beatmapId);
                }
                else
                {
                    beatmapBytes = BeatmapsCaching.Instance.GetCachedBeatmapContentAsByteArray(beatmapId);
                }

                bool beatmapHasAllHitobjects = scoreStatistics == null;
                WorkingBeatmap workingBeatmap = _currentWorkingBeatmap ?? ParseBeatmap(beatmapBytes,
                    scoreStatistics == null ? null : GetHitObjectsCountForGivenStatistics(scoreStatistics));
                IBeatmap playableBeatmap = _currentPlayableBeatmap ??
                                           workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, scoreMods,
                                               cancellationToken);
                if (beatmapHasAllHitobjects)
                {
                    _currentWorkingBeatmap = workingBeatmap;
                    _currentPlayableBeatmap = playableBeatmap;
                }


                // Get score processor
                ScoreProcessor? scoreProcessor = _currentScoreProcessor;
                if (scoreProcessor == null)
                {
                    scoreProcessor = ruleset.CreateScoreProcessor();
                    scoreProcessor.Mods.Value = scoreMods;
                    scoreProcessor.ApplyBeatmap(playableBeatmap);
                }

                if (beatmapHasAllHitobjects)
                {
                    _currentScoreProcessor = scoreProcessor;
                }


                // Set score maximum statistics
                scoreMaxStatistics ??= scoreProcessor.MaximumStatistics;


                // Get score info
                if (scoreStatistics is null)
                {
                    accuracy ??= 1;
                    scoreStatistics ??= rulesetId switch
                    {
                        0 => AccuracyTools.Osu.GenerateHitResults(playableBeatmap, scoreMods, accuracy.Value * 100),
                        1 => AccuracyTools.Taiko.GenerateHitResults(playableBeatmap, scoreMods, accuracy.Value * 100),
                        2 => AccuracyTools.Catch.GenerateHitResults(playableBeatmap, scoreMods, accuracy.Value * 100),
                        3 => AccuracyTools.Mania.GenerateHitResults(playableBeatmap, scoreMods, accuracy.Value * 100),
                        _ => throw new NotImplementedException()
                    };
                }
                else
                {
                    accuracy ??= CalculateAccuracy(scoreStatistics, scoreMaxStatistics, scoreProcessor);
                }

                scoreMaxCombo ??= scoreProcessor.MaximumCombo;
                var scoreInfo = new ScoreInfo
                {
                    Accuracy = accuracy.Value,
                    Mods = scoreMods,
                    MaxCombo = scoreMaxCombo.Value,
                    Statistics = scoreStatistics,
                    BeatmapInfo = playableBeatmap.BeatmapInfo,
                };

                // Calculate pp
                var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
                DifficultyAttributes? difficultyAttributes = null;
                if (beatmapHasAllHitobjects)
                {
                    CachedDifficultyAttrbiutes.TryGetValue(beatmapId, out difficultyAttributes);
                    if (difficultyAttributes == null)
                    {
                        difficultyAttributes = difficultyCalculator.Calculate(cancellationToken);
                    }

                    CachedDifficultyAttrbiutes.AddOrUpdate(beatmapId, difficultyAttributes,
                        (_, _) => difficultyAttributes);
                }
                else
                {
                    difficultyAttributes = difficultyCalculator.Calculate(cancellationToken);
                }


                var ppCalculator = ruleset.CreatePerformanceCalculator()!;
                PerformanceAttributes ppAttributes =
                    await ppCalculator.CalculateAsync(scoreInfo, difficultyAttributes!, cancellationToken);
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
            int miss, meh, ok, good, great, perfect;
            statistics.TryGetValue(HitResult.Miss, out miss);
            statistics.TryGetValue(HitResult.Meh, out meh);
            statistics.TryGetValue(HitResult.Ok, out ok);
            statistics.TryGetValue(HitResult.Good, out good);
            statistics.TryGetValue(HitResult.Great, out great);
            statistics.TryGetValue(HitResult.Perfect, out perfect);
            return miss + meh + ok + good + great + perfect;
        }

        private WorkingBeatmap ParseBeatmap(byte[] beatmapBytes, int? hitObjectsLimit = null)
        {
            try
            {
                // Create a working beatmap from the file
                using var stream = new MemoryStream(beatmapBytes);
                using var streamReader = new LineBufferedReader(stream);

                string versionText = UTF32Encoding.Default.GetString(beatmapBytes[..30]);
                int version = int.Parse(Regex.Match(versionText, @"v(?<ver>\d+)").Groups["ver"].Value);

                var decoder = new LegacyBeatmapDecoder(version);
                var beatmap = decoder.Decode(streamReader);

                if (hitObjectsLimit != null)
                {
                    beatmap.HitObjects = beatmap.HitObjects.Take(hitObjectsLimit.Value).ToList();
                }

                return new LoadedBeatmap(beatmap);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing beatmap: {ex.Message}");
                throw;
            }
        }

        private double CalculateAccuracy(Dictionary<HitResult, int> statistics,
            Dictionary<HitResult, int> maxStatistics, ScoreProcessor scoreProcessor)
            => StandardisedScoreMigrationTools.ComputeAccuracy(statistics, maxStatistics, scoreProcessor);
    }

    // Simple implementation of WorkingBeatmap for the calculator
    public class LoadedBeatmap : WorkingBeatmap
    {
        private readonly IBeatmap beatmap;

        public LoadedBeatmap(IBeatmap beatmap) : base(beatmap.BeatmapInfo, null)
        {
            this.beatmap = beatmap;
        }

        public override Stream? GetStream(string storagePath) => null;
        public override Texture? GetBackground() => null;

        protected override IBeatmap GetBeatmap() => beatmap;
        protected override Track? GetBeatmapTrack() => null;
        protected override ISkin? GetSkin() => null;
    }
}