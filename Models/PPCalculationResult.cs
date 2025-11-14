// ReSharper disable InconsistentNaming
using osu.Game.Rulesets.Difficulty;

namespace SosuBot.PerformanceCalculator.Models;

public record PPCalculationResult
{
    public required double Pp { get; set; }
    public required double CalculatedAccuracy { get; set; }
    public required DifficultyAttributes DifficultyAttributes { get; set; }
}