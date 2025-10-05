// ReSharper disable InconsistentNaming
namespace SosuBot.PerformanceCalculator.Models;

public record PPCalculationResult
{
    public required double Pp { get; set; }
    public required double CalculatedAccuracy { get; set; }
}