namespace SosuBot.PerformanceCalculator;

/// <summary>
/// Performance and difficulty values returned by rosu-pp.
/// </summary>
public sealed record PerformanceResult
{
    public required double PerformancePoints { get; init; }

    public required double StarRating { get; init; }

    public required uint MaxCombo { get; init; }

    public required GameMode Mode { get; init; }

    /// <summary>
    /// Accuracy represented by the supplied score state or accuracy scenario,
    /// expressed as a percentage from 0 to 100.
    /// </summary>
    public double CalculatedAccuracy { get; init; }

    public double? AimPerformance { get; init; }

    public double? SpeedPerformance { get; init; }

    public double? AccuracyPerformance { get; init; }

    public double? FlashlightPerformance { get; init; }

    // Difficulty details are primarily useful to presentation code such as
    // profile cards. They are optional because rosu only exposes these fields
    // for osu!standard difficulty attributes.
    public double? AimDifficulty { get; init; }

    public double? SpeedDifficulty { get; init; }

    public double? SpeedNoteCount { get; init; }

    public double? ApproachRate { get; init; }

    public double? OverallDifficulty { get; init; }

    public double? DrainRate { get; init; }

    public int? HitCircleCount { get; init; }

    public int? SliderCount { get; init; }
}
