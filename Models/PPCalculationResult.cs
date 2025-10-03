namespace SosuBot.PerformanceCalculator;

public record PPCalculationResult
{
    public required double Pp { get; set; }
    public required double CalculatedAccuracy { get; set; }
}