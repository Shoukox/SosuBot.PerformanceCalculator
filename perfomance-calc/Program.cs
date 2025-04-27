using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace PerfomanceCalculator
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            int beatmapId = 1893461;
            int countPerfect = 0;
            int count300 = 62;
            int countGood = 0;
            int count100 = 22;
            int count50 = 3;
            int largeTickHit = 1;
            int smallTickHit = 0;
            int largeTickMiss = 2;
            int smallTickMiss = 0;
            int countIgnoreHit = 43;
            int countIgnoreMiss = 23;
            int countSliderTailHit = 33;
            int largeBonus = 1;
            int smallBonus = 14;
            int missCount = 12;
            int maxCombo = 36;
            double accuracy = 1;

            var calculator = new PPCalculator();
            double pp = await calculator.CalculatePPAsync(
                        beatmapId,
                        maxCombo,
                        [],
                        accuracy,
                        countPerfect: countPerfect,
                        count300: count300,
                        countGood: countGood,
                        count100: count100,
                        count50: count50,
                        largeTickHit: largeTickHit,
                        smallTickHit: smallTickHit,
                        largeTickMiss: largeTickMiss,
                        smallTickMiss: smallTickMiss,
                        countIgnoreHit: countIgnoreHit,
                        countIgnoreMiss: countIgnoreMiss,
                        countSliderTailHit: countSliderTailHit,
                        largeBonus: largeBonus,
                        smallBonus: smallBonus,
                        missCount: missCount,
                        rulesetId: 0);

            Console.WriteLine("{0:F3}", pp);
        }
    }
}
