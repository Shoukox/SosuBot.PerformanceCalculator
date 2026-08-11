namespace SosuBot.PerformanceCalculator;

internal enum PerformanceCalculationType
{
    Accuracy,
    ExactScoreState,
}

internal static class PerformanceRequestValidator
{
    internal const double MinimumClockRate = 0.01;
    internal const double MaximumClockRate = 100.0;

    internal static PerformanceCalculationType Validate(
        ReadOnlyMemory<byte> beatmap,
        PerformanceRequest? request)
    {
        if (beatmap.IsEmpty)
            throw new ArgumentException("Beatmap must not be empty.", nameof(beatmap));

        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.Mode))
            throw new ArgumentOutOfRangeException(nameof(request.Mode), request.Mode,
                "The requested game mode is not supported.");

        if (request.Mods > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.Mods), request.Mods,
                "The rosu-pp mod bitmask must fit in UInt32.");

        if (request.Accuracy is { } accuracy &&
            (!double.IsFinite(accuracy) || accuracy is < 0 or > 100))
        {
            throw new ArgumentOutOfRangeException(nameof(request.Accuracy), request.Accuracy,
                "Accuracy must be finite and within 0..100.");
        }

        if (request.ClockRate is { } clockRate &&
            (!double.IsFinite(clockRate) ||
             clockRate < MinimumClockRate || clockRate > MaximumClockRate))
        {
            throw new ArgumentOutOfRangeException(nameof(request.ClockRate), request.ClockRate,
                $"Clock rate must be finite and within {MinimumClockRate}..{MaximumClockRate}.");
        }

        if (request.PassedObjects == 0)
            throw new ArgumentOutOfRangeException(nameof(request.PassedObjects), request.PassedObjects,
                "PassedObjects must be positive when supplied.");

        bool hasExactScoreState = HasExactScoreState(request);
        if (request.Accuracy is not null && hasExactScoreState)
        {
            throw new ArgumentException(
                "Accuracy cannot be combined with exact hit counts; choose one score-input scenario.",
                nameof(request));
        }

        if (request.Accuracy is null && !hasExactScoreState)
        {
            throw new ArgumentException(
                "Either Accuracy or a complete exact score state must be supplied.",
                nameof(request));
        }

        if (!hasExactScoreState)
            return PerformanceCalculationType.Accuracy;

        ValidateExactScoreState(request);
        return PerformanceCalculationType.ExactScoreState;
    }

    internal static bool HasExactScoreState(PerformanceRequest request)
    {
        return request.Count300 is not null ||
               request.Count100 is not null ||
               request.Count50 is not null ||
               request.CountGeki is not null ||
               request.CountKatu is not null ||
               request.LargeTickHits is not null ||
               request.SmallTickHits is not null ||
               request.SliderEndHits is not null ||
               request.LegacyTotalScore is not null;
    }

    private static void ValidateExactScoreState(PerformanceRequest request)
    {
        bool complete = request.Mode switch
        {
            GameMode.Osu => request.Count300 is not null &&
                            request.Count100 is not null &&
                            request.Count50 is not null &&
                            request.CountGeki is null &&
                            request.CountKatu is null,
            GameMode.Taiko => request.Count300 is not null &&
                              request.Count100 is not null &&
                              request.Count50 is null &&
                              request.CountGeki is null &&
                              request.CountKatu is null,
            GameMode.Catch => request.Count300 is not null &&
                              request.Count100 is not null &&
                              request.Count50 is not null &&
                              request.CountGeki is null &&
                              request.CountKatu is not null,
            GameMode.Mania => request.CountGeki is not null &&
                              request.Count300 is not null &&
                              request.CountKatu is not null &&
                              request.Count100 is not null &&
                              request.Count50 is not null,
            _ => false,
        };

        if (!complete)
        {
            throw new ArgumentException(
                "The exact score state is incomplete or contains fields for another mode.",
                nameof(request));
        }

        bool hasSliderStatistics = request.LargeTickHits is not null ||
                                    request.SmallTickHits is not null ||
                                    request.SliderEndHits is not null;
        if (request.Mode != GameMode.Osu && (hasSliderStatistics || request.LegacyTotalScore is not null))
        {
            throw new ArgumentException(
                "Slider statistics and legacy total score are only valid for osu!standard.",
                nameof(request));
        }

        if (!request.IsLazer && hasSliderStatistics)
        {
            throw new ArgumentException(
                "Slider statistics require IsLazer = true.",
                nameof(request));
        }

        if (request.IsLazer && request.LegacyTotalScore is not null)
        {
            throw new ArgumentException(
                "LegacyTotalScore requires IsLazer = false.",
                nameof(request));
        }

        if (request.Mode != GameMode.Osu && request.LegacyTotalScore is not null)
        {
            throw new ArgumentException(
                "LegacyTotalScore is only valid for osu!standard.",
                nameof(request));
        }

        if (request.Mode != GameMode.Mania && request.Combo is null)
        {
            throw new ArgumentException(
                "Combo is required for an exact score state in this mode.",
                nameof(request));
        }
    }
}
