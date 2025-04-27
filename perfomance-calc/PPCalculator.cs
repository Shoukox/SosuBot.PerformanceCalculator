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
        /// <returns></returns>
        public async Task<double> CalculatePPAsync(
            int beatmapId,
            int maxCombo,
            Mod[] mods,
            double accuracy,
            int countPerfect = 0,
            int count300 = 0,
            int countGood = 0,
            int count100 = 0,
            int count50 = 0,
            int largeTickHit = 0,
            int smallTickHit = 0,
            int largeTickMiss = 0,
            int smallTickMiss = 0,
            int countIgnoreHit = 0,
            int countIgnoreMiss = 0,
            int countSliderTailHit = 0,
            int smallBonus = 0,
            int largeBonus = 0,
            int missCount = 0,
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
                    Accuracy = accuracy,
                    MaxCombo = maxCombo,
                    Statistics = new Dictionary<HitResult, int>
                    {
                        { HitResult.Perfect, countPerfect },
                        { HitResult.Great, count300 },
                        { HitResult.Good, countGood },
                        { HitResult.Ok, count100 },
                        { HitResult.Meh, count50 },
                        { HitResult.Miss, missCount },
                        { HitResult.LargeTickHit, largeTickHit},
                        { HitResult.SmallTickHit, smallTickHit},
                        { HitResult.LargeTickMiss, largeTickMiss},
                        { HitResult.SmallTickMiss, smallTickMiss},
                        { HitResult.IgnoreHit, countIgnoreHit},
                        { HitResult.IgnoreMiss, countIgnoreMiss},
                        { HitResult.SliderTailHit, countSliderTailHit},
                        { HitResult.SmallBonus, smallBonus},
                        { HitResult.LargeBonus, largeBonus},
                    },

                    Ruleset = ruleset.RulesetInfo,
                    BeatmapInfo = workingBeatmap.BeatmapInfo,
                    Mods = mods
                };
                Console.WriteLine(scoreInfo.Accuracy);


                //for catch
                //num300 = score.GetCount300() ?? 0; // HitResult.Great
                //num100 = score.GetCount100() ?? 0; // HitResult.LargeTickHit
                //num50 = score.GetCount50() ?? 0; // HitResult.SmallTickHit
                //numKatu = score.GetCountKatu() ?? 0; // HitResult.SmallTickMiss
                //numMiss = score.GetCountMiss() ?? 0; // HitResult.Miss PLUS HitResult.LargeTickMiss

                // Get difficulty attributes
                var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
                var difficultyAttributes = difficultyCalculator.Calculate(mods);

                // Calculate pp
                var ppCalculator = ruleset.CreatePerformanceCalculator();
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

        /// <summary>
        /// Only for osu!std
        /// </summary>
        /// <param name="count300"></param>
        /// <param name="count100"></param>
        /// <param name="count50"></param>
        /// <param name="missCount"></param>
        /// <returns></returns>
        private double CalculateStdAccuracy(int count300, int count100, int count50, int missCount)
        {
            int totalHits = count300 + count100 + count50 + missCount;
            if (totalHits == 0) return 1.0;

            double numerator = (count300 * 300.0 + count100 * 100.0 + count50 * 50.0);
            double denominator = totalHits * 300.0;

            return numerator / denominator;
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

        public override Stream? GetStream(string storagePath) => null;
        public override Texture? GetBackground() => null;

        protected override IBeatmap GetBeatmap() => beatmap;
        protected override Track? GetBeatmapTrack() => null;
        protected override ISkin? GetSkin() => null;
    }
}
