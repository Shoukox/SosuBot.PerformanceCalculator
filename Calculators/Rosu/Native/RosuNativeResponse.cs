namespace SosuBot.PerformanceCalculator;

internal sealed record RosuNativeResponse
{
    public bool Success { get; init; }

    public RosuNativeResult? Result { get; init; }

    public RosuNativeError? Error { get; init; }
}

internal sealed record RosuNativeResult
{
    public double PerformancePoints { get; init; }

    public double StarRating { get; init; }

    public uint MaxCombo { get; init; }

    public string? Mode { get; init; }

    public double? AimPerformance { get; init; }

    public double? SpeedPerformance { get; init; }

    public double? AccuracyPerformance { get; init; }

    public double? FlashlightPerformance { get; init; }

    public double? AimDifficulty { get; init; }

    public double? SpeedDifficulty { get; init; }

    public double? SpeedNoteCount { get; init; }

    public double? ApproachRate { get; init; }

    public double? OverallDifficulty { get; init; }

    public double? DrainRate { get; init; }

    public int? HitCircleCount { get; init; }

    public int? SliderCount { get; init; }
}

internal sealed record RosuNativeError
{
    public string? Code { get; init; }

    public string? Message { get; init; }
}
