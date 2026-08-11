namespace SosuBot.PerformanceCalculator;

internal interface IRosuNativeApi
{
    void EnsureLoaded();

    nint Calculate(ReadOnlySpan<byte> beatmap, ReadOnlySpan<byte> requestJson);

    void FreeString(nint value);
}
