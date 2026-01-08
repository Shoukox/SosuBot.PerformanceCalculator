// ReSharper disable InconsistentNaming
using osu.Game.Rulesets.Difficulty;

namespace SosuBot.PerformanceCalculator;

public record PPCalculationResult
{
    public required double Pp { get; set; }
    public required double CalculatedAccuracy { get; set; }
    public required DifficultyAttributes DifficultyAttributes { get; set; }
    public required int BeatmapMaxCombo { get; set; }
    public required int BeatmapHitObjectsCount { get; set; }
    public required int ScoreHitResultsCount { get; set; }
}