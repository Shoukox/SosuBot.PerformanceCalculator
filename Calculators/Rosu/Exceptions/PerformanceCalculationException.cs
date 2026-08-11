namespace SosuBot.PerformanceCalculator;

public class PerformanceCalculationException : Exception
{
    public PerformanceCalculationException(string message)
        : base(message)
    {
    }

    public PerformanceCalculationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
