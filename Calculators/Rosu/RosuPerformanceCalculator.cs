using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SosuBot.PerformanceCalculator;

public sealed class RosuPerformanceCalculator
{
    private readonly RosuNativeClient _nativeClient;
    private readonly ILogger<RosuPerformanceCalculator>? _logger;
    private readonly IPerformanceCalculationMetrics? _metrics;

    public RosuPerformanceCalculator(
        ILogger<RosuPerformanceCalculator>? logger = null,
        IPerformanceCalculationMetrics? metrics = null)
        : this(new RosuNativeClient(), logger, metrics)
    {
    }

    internal RosuPerformanceCalculator(
        RosuNativeClient nativeClient,
        ILogger<RosuPerformanceCalculator>? logger = null,
        IPerformanceCalculationMetrics? metrics = null)
    {
        _nativeClient = nativeClient;
        _logger = logger;
        _metrics = metrics;
    }

    public PerformanceResult Calculate(
        ReadOnlyMemory<byte> beatmap,
        PerformanceRequest request)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        PerformanceCalculationType calculationType = PerformanceCalculationType.Accuracy;
        bool succeeded = false;
        string errorCode = "none";

        try
        {
            calculationType = PerformanceRequestValidator.Validate(beatmap, request);
            _nativeClient.EnsureLoaded();
            SetNativeLoadedSafely(true);
            RosuNativeRequest nativeRequest = RosuNativeRequest.From(request);
            RosuNativeResult nativeResult = _nativeClient.Calculate(beatmap, nativeRequest);
            PerformanceResult result = MapResult(request, nativeResult);
            succeeded = true;
            return result;
        }
        catch (Exception exception)
        {
            errorCode = GetErrorCode(exception);
            if (errorCode.StartsWith("NATIVE_", StringComparison.Ordinal))
                SetNativeLoadedSafely(false);

            _logger?.LogWarning(
                exception,
                "rosu-pp calculation failed: mode={Mode}, calculationType={CalculationType}, errorCode={ErrorCode}",
                request?.Mode.ToString() ?? "unknown",
                calculationType,
                errorCode);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger?.LogInformation(
                "rosu-pp calculation finished: mode={Mode}, calculationType={CalculationType}, success={Success}, durationMs={DurationMs}",
                request?.Mode.ToString() ?? "unknown",
                calculationType,
                succeeded,
                stopwatch.Elapsed.TotalMilliseconds);
            RecordMetricsSafely(
                request is null ? "unknown" : FormatMode(request.Mode),
                succeeded ? "success" : "error",
                calculationType == PerformanceCalculationType.Accuracy ? "accuracy" : "exact_score_state",
                errorCode,
                stopwatch.Elapsed.TotalSeconds);
        }
    }

    private static PerformanceResult MapResult(PerformanceRequest request, RosuNativeResult nativeResult)
    {
        if (nativeResult.Mode is null ||
            !TryParseMode(nativeResult.Mode, out GameMode nativeMode) ||
            nativeMode != request.Mode)
        {
            throw new RosuNativeException(
                "INVALID_RESPONSE",
                "rosu_pp_native returned a result for an unexpected game mode.");
        }

        if (!double.IsFinite(nativeResult.PerformancePoints) ||
            !double.IsFinite(nativeResult.StarRating) ||
            !IsFinite(nativeResult.AimPerformance) ||
            !IsFinite(nativeResult.SpeedPerformance) ||
            !IsFinite(nativeResult.AccuracyPerformance) ||
            !IsFinite(nativeResult.FlashlightPerformance))
        {
            throw new RosuNativeException(
                "INVALID_RESPONSE",
                "rosu_pp_native returned non-finite performance values.");
        }

        return new PerformanceResult
        {
            PerformancePoints = nativeResult.PerformancePoints,
            StarRating = nativeResult.StarRating,
            MaxCombo = nativeResult.MaxCombo,
            Mode = nativeMode,
            CalculatedAccuracy = PerformanceAccuracy.Calculate(request),
            AimPerformance = nativeResult.AimPerformance,
            SpeedPerformance = nativeResult.SpeedPerformance,
            AccuracyPerformance = nativeResult.AccuracyPerformance,
            FlashlightPerformance = nativeResult.FlashlightPerformance,
            AimDifficulty = nativeResult.AimDifficulty,
            SpeedDifficulty = nativeResult.SpeedDifficulty,
            SpeedNoteCount = nativeResult.SpeedNoteCount,
            ApproachRate = nativeResult.ApproachRate,
            OverallDifficulty = nativeResult.OverallDifficulty,
            DrainRate = nativeResult.DrainRate,
            HitCircleCount = nativeResult.HitCircleCount,
            SliderCount = nativeResult.SliderCount,
        };
    }

    private static bool IsFinite(double? value) => value is null || double.IsFinite(value.Value);

    private static string GetErrorCode(Exception exception)
    {
        return exception switch
        {
            RosuNativeException nativeException => nativeException.Code,
            ArgumentException => "VALIDATION_ERROR",
            _ => "UNEXPECTED_ERROR",
        };
    }

    private static string FormatMode(GameMode mode)
    {
        return mode switch
        {
            GameMode.Osu => "osu",
            GameMode.Taiko => "taiko",
            GameMode.Catch => "catch",
            GameMode.Mania => "mania",
            _ => "unknown",
        };
    }

    private void SetNativeLoadedSafely(bool loaded)
    {
        try
        {
            _metrics?.SetNativeLibraryLoaded(loaded);
        }
        catch (Exception exception)
        {
            _logger?.LogDebug(exception, "Could not update rosu-pp native-loaded metric.");
        }
    }

    private void RecordMetricsSafely(
        string mode,
        string status,
        string calculationType,
        string errorCode,
        double durationSeconds)
    {
        try
        {
            _metrics?.RecordCalculation(mode, status, calculationType, errorCode, durationSeconds);
        }
        catch (Exception exception)
        {
            _logger?.LogDebug(exception, "Could not record rosu-pp calculation metrics.");
        }
    }

    private static bool TryParseMode(string value, out GameMode mode)
    {
        mode = value.ToLowerInvariant() switch
        {
            "osu" or "standard" => GameMode.Osu,
            "taiko" => GameMode.Taiko,
            "catch" or "fruits" => GameMode.Catch,
            "mania" => GameMode.Mania,
            _ => (GameMode)(-1),
        };

        return Enum.IsDefined(mode);
    }
}
