using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Beatmaps.Formats;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.IO;
using osu.Game.Skinning;
using System.Text;
using System.Text.RegularExpressions;
using osu.Framework.Logging;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Catch;
using osu.Game.Database;

namespace PerfomanceCalculator
{
    public class PPCalculator
    {
        private readonly HttpClient httpClient;
        private const string BEATMAP_DOWNLOAD_URL = "https://osu.ppy.sh/osu/";

        public PPCalculator()
        {
            httpClient = new HttpClient();

            Logger.Level = LogLevel.Error;
        }

        /// <summary>
        /// Calculates pp for given ruleset
        /// </summary>
        /// <param name="beatmapId">Beatmap ID</param>
        /// <param name="maxCombo">Score's max combo</param>
        /// <param name="mods">enabled mods in the score</param>
        /// <param name="accuracy">You should get this value from osu!api if working with lazer scores. Otherwise you can just calculate accuracy if working with osu!stable</param>
        /// <param name="countPerfect">Only for mania</param>
        /// <param name="count300">Equals Great</param>
        /// <param name="count100">Equals Ok, LargeTickHit</param>
        /// <param name="countGood">Only for mania</param>
        /// <param name="count50">Equals Meh, SmallTickHit</param>
        /// <param name="largeTickMiss">Only for catch</param>
        /// <param name="smallTickMiss">Only for catch</param>
        /// <param name="missCount">Score's miss count</param>
        /// <param name="rulesetId">
        ///         The play mode for pp calculation.
        ///         Std = 0,
        ///         Taiko = 1,
        ///         Catch = 2,
        ///         Mania = 3
        /// </param>
        /// <returns>pp and accuracy</returns>
        public async Task<double> CalculatePPAsync(
            int beatmapId,
            int maxCombo,
            Mod[] mods,
            Dictionary<HitResult, int> statistics,
            Dictionary<HitResult, int> maxStatistics,
            int rulesetId = 0)
        {
            try
            {
                var beatmapBytes = await DownloadBeatmapAsync(beatmapId);
                var workingBeatmap = ParseBeatmap(beatmapBytes);
                if (workingBeatmap == null)
                    throw new Exception("Failed to parse beatmap");

                Ruleset ruleset = rulesetId switch
                {
                    0 => new OsuRuleset(),
                    1 => new TaikoRuleset(),
                    2 => new CatchRuleset(),
                    3 => new ManiaRuleset(),
                    _ => new OsuRuleset()
                };

                var scoreInfo = new ScoreInfo
                {
                    Accuracy = CalculateAccuracy(statistics, maxStatistics, ruleset.CreateScoreProcessor()),
                    MaxCombo = maxCombo,
                    Statistics = statistics,

                    Ruleset = ruleset.RulesetInfo,
                    BeatmapInfo = workingBeatmap.BeatmapInfo,
                    Mods = mods
                };

                // Get difficulty attributes
                var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
                var difficultyAttributes = difficultyCalculator.Calculate(mods);

                // Calculate pp
                var ppCalculator = ruleset.CreatePerformanceCalculator()!;
                var ppAttributes = ppCalculator.Calculate(scoreInfo, difficultyAttributes);

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

        private async Task<byte[]> DownloadBeatmapAsync(int beatmapId)
        {
            var response = await httpClient.GetAsync($"{BEATMAP_DOWNLOAD_URL}{beatmapId}");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to download beatmap {beatmapId}. Status code: {response.StatusCode}");

            return await response.Content.ReadAsByteArrayAsync();
        }

        private WorkingBeatmap ParseBeatmap(byte[] beatmapBytes)
        {
            try
            {
                // Create a working beatmap from the file
                using var stream = new MemoryStream(beatmapBytes);
                using var streamReader = new LineBufferedReader(stream);

                string versionText = UTF32Encoding.Default.GetString(beatmapBytes[..20]);
                int version = int.Parse(Regex.Match(versionText, @"v(?<ver>\d+)").Groups["ver"].Value);

                var decoder = new LegacyBeatmapDecoder(version);
                var beatmap = decoder.Decode(streamReader);

                return new LoadedBeatmap(beatmap);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing beatmap: {ex.Message}");
                throw;
            }
        }

        private double CalculateAccuracy(Dictionary<HitResult, int> statistics, Dictionary<HitResult, int> maxStatistics, ScoreProcessor scoreProcessor)
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
