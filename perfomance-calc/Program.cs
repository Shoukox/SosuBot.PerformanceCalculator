using osu.Game.Rulesets.Osu.Mods;

namespace PerfomanceCalculator
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            int beatmapId = 970048;
            int count300 = 1130;
            int count100 = 19;
            int count50 = 0;
            int missCount = 0;
            int maxCombo = 1466;

            var calculator = new PPCalculator();
            double pp = await calculator.CalculatePP(
                beatmapId,
                count300,
                count100,
                count50,
                missCount,
                maxCombo,
                [new OsuModClassic()]);

            Console.WriteLine("{0:0.000000}", pp);
        }
    }
}
