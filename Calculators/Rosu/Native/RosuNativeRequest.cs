namespace SosuBot.PerformanceCalculator;

internal sealed record RosuNativeRequest
{
    public required string Mode { get; init; }

    public ulong Mods { get; init; }

    public double? Accuracy { get; init; }

    public uint? Combo { get; init; }

    public uint Misses { get; init; }

    public uint? Count300 { get; init; }

    public uint? Count100 { get; init; }

    public uint? Count50 { get; init; }

    public uint? CountGeki { get; init; }

    public uint? CountKatu { get; init; }

    public uint? PassedObjects { get; init; }

    public double? ClockRate { get; init; }

    public bool IsLazer { get; init; }

    public uint? LargeTickHits { get; init; }

    public uint? SmallTickHits { get; init; }

    public uint? SliderEndHits { get; init; }

    public uint? LegacyTotalScore { get; init; }

    internal static RosuNativeRequest From(PerformanceRequest request)
    {
        return new RosuNativeRequest
        {
            Mode = request.Mode switch
            {
                GameMode.Osu => "osu",
                GameMode.Taiko => "taiko",
                GameMode.Catch => "catch",
                GameMode.Mania => "mania",
                _ => throw new ArgumentOutOfRangeException(nameof(request.Mode)),
            },
            Mods = request.Mods,
            Accuracy = request.Accuracy,
            Combo = request.Combo,
            Misses = request.Misses,
            Count300 = request.Count300,
            Count100 = request.Count100,
            Count50 = request.Count50,
            CountGeki = request.CountGeki,
            CountKatu = request.CountKatu,
            PassedObjects = request.PassedObjects,
            ClockRate = request.ClockRate,
            IsLazer = request.IsLazer,
            LargeTickHits = request.LargeTickHits,
            SmallTickHits = request.SmallTickHits,
            SliderEndHits = request.SliderEndHits,
            LegacyTotalScore = request.LegacyTotalScore,
        };
    }
}
