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
            int missCount = 0;
            int maxCombo = 1466;

            var calculator = new PPCalculator();
            double pp = await calculator.CalculatePPAsync(
                beatmapId, 
                maxCombo, 
                [new CatchModClassic()],



            Console.WriteLine("{0:F3}", pp);
        }
    }
}
