using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace PerfomanceCalculator
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            int beatmapId = 970048;
            int countPerfect = 0;
            int count300 = 1130;
            int countGood = 0;
            int count100 = 19;
            int count50 = 0;
            int largeTickMiss = 0;
            int smallTickMiss = 0;
            int missCount = 0;
            int maxCombo = 1466;

            var calculator = new PPCalculator();
            double pp = await calculator.CalculatePPAsync(
                        beatmapId,
                        maxCombo,
                        [new OsuModClassic()],
                        countPerfect: countPerfect,
                        count300: count300,
                        countGood: countGood,
                        count100: count100,
                        count50: count50,
                        largeTickMiss: largeTickMiss,
                        smallTickMiss: smallTickMiss,
                        missCount: missCount,
                        rulesetId: 0);

            Console.WriteLine("{0:F3}", pp);
        }
    }
}
