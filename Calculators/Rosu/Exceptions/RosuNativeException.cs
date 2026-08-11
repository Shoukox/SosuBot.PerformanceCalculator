namespace SosuBot.PerformanceCalculator;

public sealed class RosuNativeException : PerformanceCalculationException
{
    public RosuNativeException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public RosuNativeException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
