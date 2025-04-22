using osu.Game.Rulesets.Osu.Mods;

namespace PerfomanceCalculator
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            int beatmapId = 4975644;
            int count300 = 2436;
            int count100 = 89;
            int count50 = 0;
            int missCount = 0;
            int maxCombo = 3409;
            double accuracy = 0.9765;

            var calculator = new PPCalculator();
            double pp = await calculator.CalculatePP(
                beatmapId,
                count300,
                count100,
                count50,
                missCount,
                maxCombo,
                accuracy,
                [new OsuModDoubleTime(), new OsuModHidden(), new OsuModClassic()]);

            Console.WriteLine("{0:F2}", pp);
        }
    }
}
