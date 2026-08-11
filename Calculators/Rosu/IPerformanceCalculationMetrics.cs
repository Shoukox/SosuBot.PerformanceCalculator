namespace SosuBot.PerformanceCalculator;

/// <summary>
/// Optional metrics sink used by the host application.
/// </summary>
public interface IPerformanceCalculationMetrics
{
    void SetNativeLibraryLoaded(bool loaded);

    void RecordCalculation(
        string mode,
        string status,
        string calculationType,
        string errorCode,
        double durationSeconds);
}
