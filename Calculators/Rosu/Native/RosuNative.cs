using System.Reflection;
using System.Runtime.InteropServices;

namespace SosuBot.PerformanceCalculator;

internal static unsafe partial class RosuNative
{
    private const string LibraryName = "rosu_pp_native";
    private static readonly object LoadLock = new();
    private static nint _libraryHandle;

    static RosuNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(RosuNative).Assembly, ResolveLibrary);
    }

    [LibraryImport(LibraryName, EntryPoint = "rosu_calculate")]
    internal static partial nint Calculate(
        byte* beatmapPtr,
        nuint beatmapLength,
        byte* requestJsonPtr,
        nuint requestJsonLength);

    [LibraryImport(LibraryName, EntryPoint = "rosu_free_string")]
    internal static partial void FreeString(nint value);

    internal static void EnsureLoaded()
    {
        if (Volatile.Read(ref _libraryHandle) != 0)
            return;

        lock (LoadLock)
        {
            if (_libraryHandle != 0)
                return;

            Exception? lastException = null;
            foreach (string path in GetCandidatePaths().Distinct(StringComparer.Ordinal))
            {
                try
                {
                    if (NativeLibrary.TryLoad(path, out nint handle))
                    {
                        if (!HasRequiredExports(handle))
                        {
                            NativeLibrary.Free(handle);
                            lastException = new EntryPointNotFoundException(
                                $"{Path.GetFileName(path)} does not expose the required rosu-pp C ABI.");
                            continue;
                        }

                        Volatile.Write(ref _libraryHandle, handle);
                        return;
                    }
                }
                catch (Exception exception) when (exception is DllNotFoundException or
                                                  BadImageFormatException or
                                                  EntryPointNotFoundException)
                {
                    lastException = exception;
                }
            }

            try
            {
                nint handle = NativeLibrary.Load(LibraryName);
                if (!HasRequiredExports(handle))
                {
                    NativeLibrary.Free(handle);
                    throw new EntryPointNotFoundException(
                        "rosu_pp_native does not expose rosu_calculate and rosu_free_string.");
                }

                Volatile.Write(ref _libraryHandle, handle);
            }
            catch (Exception exception) when (exception is DllNotFoundException or
                                              BadImageFormatException or
                                              EntryPointNotFoundException)
            {
                throw new RosuNativeException(
                    "NATIVE_LIBRARY_LOAD_ERROR",
                    "Could not load rosu_pp_native. Build the native wrapper and publish the correct runtime asset.",
                    lastException ?? exception);
            }
        }
    }

    private static bool HasRequiredExports(nint handle)
    {
        return NativeLibrary.TryGetExport(handle, "rosu_calculate", out _) &&
               NativeLibrary.TryGetExport(handle, "rosu_free_string", out _);
    }

    private static nint ResolveLibrary(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
            return nint.Zero;

        try
        {
            EnsureLoaded();
            return Volatile.Read(ref _libraryHandle);
        }
        catch (RosuNativeException)
        {
            return nint.Zero;
        }
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        string fileName = GetLibraryFileName();
        string rid = GetRuntimeIdentifier();
        string baseDirectory = AppContext.BaseDirectory;
        string assemblyDirectory = Path.GetDirectoryName(typeof(RosuNative).Assembly.Location)
                                    ?? baseDirectory;

        foreach (string root in new[] { baseDirectory, assemblyDirectory })
        {
            yield return Path.Combine(root, "runtimes", rid, "native", fileName);
            yield return Path.Combine(root, fileName);
        }
    }

    private static string GetRuntimeIdentifier()
    {
        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new RosuNativeException(
                "NATIVE_LIBRARY_LOAD_ERROR",
                "rosu_pp_native is only packaged for linux-x64, linux-arm64 and win-x64."),
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"linux-{architecture}";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && architecture == "x64")
            return "win-x64";

        throw new RosuNativeException(
            "NATIVE_LIBRARY_LOAD_ERROR",
            "rosu_pp_native is only packaged for linux-x64, linux-arm64 and win-x64.");
    }

    private static string GetLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "rosu_pp_native.dll";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "librosu_pp_native.dylib";

        return "librosu_pp_native.so";
    }
}
