namespace SosuBot.PerformanceCalculator;

internal static class PerformanceAccuracy
{
    internal static double Calculate(PerformanceRequest request)
    {
        if (request.Accuracy is { } accuracy)
            return accuracy;

        double value = request.Mode switch
        {
            GameMode.Osu => CalculateOsu(request),
            GameMode.Taiko => CalculateTaiko(request),
            GameMode.Catch => CalculateCatch(request),
            GameMode.Mania => CalculateMania(request),
            _ => 0,
        };

        return double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
    }

    private static double CalculateOsu(PerformanceRequest request)
    {
        double great = request.Count300.GetValueOrDefault();
        double good = request.Count100.GetValueOrDefault();
        double meh = request.Count50.GetValueOrDefault();
        double misses = request.Misses;
        double total = great + good + meh + misses;
        return total <= 0 ? 0 : (6 * great + 2 * good + meh) / (6 * total) * 100;
    }

    private static double CalculateTaiko(PerformanceRequest request)
    {
        double great = request.Count300.GetValueOrDefault();
        double good = request.Count100.GetValueOrDefault();
        double misses = request.Misses;
        double total = great + good + misses;
        return total <= 0 ? 0 : (2 * great + good) / (2 * total) * 100;
    }

    private static double CalculateCatch(PerformanceRequest request)
    {
        double fruits = request.Count300.GetValueOrDefault();
        double droplets = request.Count100.GetValueOrDefault();
        double tinyDroplets = request.Count50.GetValueOrDefault();
        double tinyMisses = request.CountKatu.GetValueOrDefault();
        double misses = request.Misses;
        double total = fruits + droplets + tinyDroplets + tinyMisses + misses;
        return total <= 0 ? 0 : (fruits + droplets + tinyDroplets) / total * 100;
    }

    private static double CalculateMania(PerformanceRequest request)
    {
        double perfect = request.CountGeki.GetValueOrDefault();
        double great = request.Count300.GetValueOrDefault();
        double good = request.CountKatu.GetValueOrDefault();
        double ok = request.Count100.GetValueOrDefault();
        double meh = request.Count50.GetValueOrDefault();
        double misses = request.Misses;
        double perfectWeight = request.IsLazer ? 305 : 300;
        double total = perfect + great + good + ok + meh + misses;
        return total <= 0
            ? 0
            : (perfectWeight * perfect + 300 * great + 200 * good + 100 * ok + 50 * meh)
              / (perfectWeight * total) * 100;
    }
}
