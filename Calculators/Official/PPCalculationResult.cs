// ReSharper disable InconsistentNaming
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Scoring;

namespace SosuBot.PerformanceCalculator;

public record PPCalculationResult
{
    public required double PP { get; set; }
    public required double CalculatedAccuracy { get; set; }
    public required DifficultyAttributes DifficultyAttributes { get; set; }
    public required PerformanceAttributes PerformanceAttributes { get; set; }
    public required int BeatmapMaxCombo { get; set; }
    public required int BeatmapHitObjectsCount { get; set; }
    public required int ScoreHitResultsCount { get; set; }
    public required double CS { get; set; }
    public required double HP { get; set; }
    public required double OD { get; set; }
    public required double AR { get; set; }
    public required double SpeedChangeFactor { get; set; }
    public Dictionary<HitResult, int> ScoreStatistics { get; init; } = [];
}
