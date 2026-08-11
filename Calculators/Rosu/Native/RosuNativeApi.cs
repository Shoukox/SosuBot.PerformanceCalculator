namespace SosuBot.PerformanceCalculator;

/// <summary>
/// The only managed layer that pins buffers and passes raw pointers over the C ABI.
/// </summary>
internal sealed unsafe class RosuNativeApi : IRosuNativeApi
{
    public void EnsureLoaded() => RosuNative.EnsureLoaded();

    public nint Calculate(ReadOnlySpan<byte> beatmap, ReadOnlySpan<byte> requestJson)
    {
        fixed (byte* beatmapPointer = beatmap)
        fixed (byte* requestPointer = requestJson)
        {
            return RosuNative.Calculate(
                beatmapPointer,
                (nuint)beatmap.Length,
                requestPointer,
                (nuint)requestJson.Length);
        }
    }

    public void FreeString(nint value) => RosuNative.FreeString(value);
}
