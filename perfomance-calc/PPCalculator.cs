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

        public async Task<double> CalculatePP(
            int beatmapId,
            int count300,
            int count100,
            int count50,
            int missCount,
            int maxCombo,
            Mod[] mods)
        {
            try
            {
                var beatmapBytes = await DownloadBeatmapAsync(beatmapId);
                var workingBeatmap = ParseBeatmap(beatmapBytes);
                if (workingBeatmap == null)
                    throw new Exception("Failed to parse beatmap");

                var ruleset = new OsuRuleset();
                var scoreInfo = new ScoreInfo
                {
                    Accuracy = CalculateAccuracy(count300, count100, count50, missCount),
                    MaxCombo = maxCombo,
                    Statistics = new Dictionary<HitResult, int>
                    {
                        { HitResult.Great, count300 },
                        { HitResult.Ok, count100 },
                        { HitResult.Meh, count50 },
                        { HitResult.Miss, missCount },
                    },
                    Ruleset = ruleset.RulesetInfo,
                    BeatmapInfo = workingBeatmap.BeatmapInfo,
                    Mods = mods
                };

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

        private double CalculateAccuracy(int count300, int count100, int count50, int missCount)
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
