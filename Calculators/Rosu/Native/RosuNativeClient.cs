using System.Runtime.InteropServices;
using System.Text.Json;

namespace SosuBot.PerformanceCalculator;

internal sealed class RosuNativeClient
{
    private readonly IRosuNativeApi _nativeApi;

    internal RosuNativeClient(IRosuNativeApi? nativeApi = null)
    {
        _nativeApi = nativeApi ?? new RosuNativeApi();
    }

    internal void EnsureLoaded() => _nativeApi.EnsureLoaded();

    internal RosuNativeResult Calculate(
        ReadOnlyMemory<byte> beatmap,
        RosuNativeRequest request)
    {
        EnsureLoaded();

        byte[] requestJson = JsonSerializer.SerializeToUtf8Bytes(
            request,
            RosuJsonContext.Default.RosuNativeRequest);

        nint responsePointer = nint.Zero;
        try
        {
            responsePointer = _nativeApi.Calculate(beatmap.Span, requestJson);

            if (responsePointer == nint.Zero)
            {
                throw new RosuNativeException(
                    "NATIVE_NULL_RESPONSE",
                    "rosu_pp_native returned a null response pointer.");
            }

            string responseJson = Marshal.PtrToStringUTF8(responsePointer)
                                  ?? throw new RosuNativeException(
                                      "INVALID_RESPONSE",
                                      "rosu_pp_native returned an invalid UTF-8 response.");
            RosuNativeResponse? response = JsonSerializer.Deserialize(
                responseJson,
                RosuJsonContext.Default.RosuNativeResponse);

            if (response is null)
            {
                throw new RosuNativeException(
                    "INVALID_RESPONSE",
                    "rosu_pp_native returned an empty response envelope.");
            }

            if (!response.Success)
            {
                if (response.Result is not null)
                {
                    throw new RosuNativeException(
                        "INVALID_RESPONSE",
                        "rosu_pp_native returned an error envelope containing a result.");
                }

                string code = response.Error?.Code ?? "NATIVE_ERROR";
                string message = response.Error?.Message ?? "rosu_pp_native returned an unspecified error.";
                throw new RosuNativeException(code, message);
            }

            if (response.Error is not null)
            {
                throw new RosuNativeException(
                    "INVALID_RESPONSE",
                    "rosu_pp_native returned a success envelope containing an error.");
            }

            return response.Result ?? throw new RosuNativeException(
                "INVALID_RESPONSE",
                "rosu_pp_native returned success without a result.");
        }
        catch (RosuNativeException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new RosuNativeException(
                "INVALID_RESPONSE",
                "rosu_pp_native returned malformed JSON.",
                exception);
        }
        catch (DllNotFoundException exception)
        {
            throw new RosuNativeException(
                "NATIVE_LIBRARY_LOAD_ERROR",
                "rosu_pp_native could not be loaded.",
                exception);
        }
        catch (BadImageFormatException exception)
        {
            throw new RosuNativeException(
                "NATIVE_LIBRARY_ARCHITECTURE_ERROR",
                "rosu_pp_native has an incompatible architecture.",
                exception);
        }
        catch (EntryPointNotFoundException exception)
        {
            throw new RosuNativeException(
                "NATIVE_ENTRY_POINT_ERROR",
                "rosu_pp_native does not expose the required C ABI entry point.",
                exception);
        }
        finally
        {
            if (responsePointer != nint.Zero)
                _nativeApi.FreeString(responsePointer);
        }
    }
}
