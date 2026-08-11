using OsuApi.BanchoV2.Models;

namespace SosuBot.PerformanceCalculator;

/// <summary>
/// Maps the score model already used by SosuBot into an exact rosu-pp score state.
/// </summary>
public static class PerformanceRequestMapper
{
    /// <summary>
    /// Converts osu! API mod acronyms to the legacy bitmask accepted by rosu-pp.
    /// Returns <see langword="false"/> when a mod has no lossless legacy
    /// representation (for example lazer-only settings or Classic).
    /// </summary>
    public static bool TryGetRosuModBitmask(
        IEnumerable<OsuApi.BanchoV2.Models.Mod>? mods,
        out ulong bitmask)
    {
        return TryGetRosuModBitmask(mods?.Select(mod => mod.Acronym), out bitmask);
    }

    /// <summary>
    /// Converts a sequence of mod acronyms to the legacy bitmask accepted by
    /// rosu-pp. This overload is used by callers that already have osu!lazer
    /// mod instances instead of API models.
    /// </summary>
    public static bool TryGetRosuModBitmask(
        IEnumerable<string?>? modAcronyms,
        out ulong bitmask)
    {
        bitmask = 0;

        if (modAcronyms is null)
            return true;

        foreach (string? modAcronym in modAcronyms)
        {
            string? acronym = modAcronym?.ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(acronym) || acronym == "NM")
                continue;

            if (!TryGetLegacyModBits(acronym, out uint bits))
            {
                bitmask = 0;
                return false;
            }

            bitmask |= bits;
        }

        return true;
    }

    /// <summary>
    /// Creates an exact score-state request. The legacy mod bitmask remains an
    /// explicit argument because osu! API mod settings are not losslessly
    /// representable by that bitmask.
    /// </summary>
    public static PerformanceRequest ToPerformanceRequest(
        this Score score,
        ulong mods,
        uint? passedObjects = null,
        double? clockRate = null)
    {
        ArgumentNullException.ThrowIfNull(score);

        ScoreStatistics statistics = score.Statistics
            ?? throw new ArgumentException("Score statistics are required.", nameof(score));
        GameMode mode = ParseMode(score);
        bool isLazer = score.LegacyScoreId is null;

        var request = new PerformanceRequest
        {
            Mode = mode,
            Mods = mods,
            Combo = ToNullableUInt32(score.MaxCombo, nameof(score.MaxCombo)),
            Misses = ToUInt32(statistics.Miss, nameof(statistics.Miss)),
            PassedObjects = passedObjects,
            ClockRate = clockRate,
            IsLazer = isLazer,
        };

        return mode switch
        {
            GameMode.Osu => request with
            {
                Count300 = ToUInt32(statistics.Great, nameof(statistics.Great)),
                Count100 = ToUInt32(statistics.Ok, nameof(statistics.Ok)),
                Count50 = ToUInt32(statistics.Meh, nameof(statistics.Meh)),
                LargeTickHits = isLazer
                    ? ToUInt32(statistics.LargeTickHit, nameof(statistics.LargeTickHit))
                    : null,
                SmallTickHits = isLazer
                    ? ToUInt32(statistics.SmallTickHit, nameof(statistics.SmallTickHit))
                    : null,
                SliderEndHits = isLazer
                    ? ToUInt32(statistics.SliderTailHit, nameof(statistics.SliderTailHit))
                    : null,
                LegacyTotalScore = !isLazer
                    ? ToNullableUInt32(score.LegacyTotalScore, nameof(score.LegacyTotalScore))
                    : null,
            },
            GameMode.Taiko => request with
            {
                Count300 = ToUInt32(statistics.Great, nameof(statistics.Great)),
                Count100 = ToUInt32(statistics.Ok, nameof(statistics.Ok)),
            },
            GameMode.Catch => request with
            {
                Count300 = ToUInt32(statistics.Great, nameof(statistics.Great)),
                Count100 = ToUInt32(statistics.LargeTickHit, nameof(statistics.LargeTickHit)),
                Count50 = ToUInt32(statistics.SmallTickHit, nameof(statistics.SmallTickHit)),
                CountKatu = ToUInt32(statistics.SmallTickMiss, nameof(statistics.SmallTickMiss)),
            },
            GameMode.Mania => request with
            {
                CountGeki = ToUInt32(statistics.Perfect, nameof(statistics.Perfect)),
                Count300 = ToUInt32(statistics.Great, nameof(statistics.Great)),
                CountKatu = ToUInt32(statistics.Good, nameof(statistics.Good)),
                Count100 = ToUInt32(statistics.Ok, nameof(statistics.Ok)),
                Count50 = ToUInt32(statistics.Meh, nameof(statistics.Meh)),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(score), "Unsupported score mode."),
        };
    }

    private static GameMode ParseMode(Score score)
    {
        int? numericMode = score.RulesetId ?? score.ModeInt;
        if (numericMode is >= 0 and <= 3)
            return (GameMode)numericMode.Value;

        return score.Mode?.ToLowerInvariant() switch
        {
            "osu" or "standard" => GameMode.Osu,
            "taiko" => GameMode.Taiko,
            "catch" or "fruits" => GameMode.Catch,
            "mania" => GameMode.Mania,
            _ => throw new ArgumentException("Score mode is missing or unsupported.", nameof(score)),
        };
    }

    private static uint ToUInt32(int value, string parameterName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameterName, value, "Score counts cannot be negative.");

        return (uint)value;
    }

    private static uint? ToNullableUInt32(int? value, string parameterName)
    {
        return value is null ? null : ToUInt32(value.Value, parameterName);
    }

    private static uint? ToNullableUInt32(ulong? value, string parameterName)
    {
        if (value is null)
            return null;

        if (value.Value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value.Value,
                "The native rosu-pp score-state field must fit in UInt32.");
        }

        return (uint)value.Value;
    }

    private static bool TryGetLegacyModBits(string acronym, out uint bits)
    {
        bits = acronym switch
        {
            "NF" => 1u << 0,
            "EZ" => 1u << 1,
            "TD" => 1u << 2,
            "HD" => 1u << 3,
            "HR" => 1u << 4,
            "SD" => 1u << 5,
            "DT" => 1u << 6,
            "RX" or "RL" => 1u << 7,
            "HT" or "DC" => 1u << 8,
            "NC" => (1u << 9) | (1u << 6),
            "FL" => 1u << 10,
            "AT" => 1u << 11,
            "SO" => 1u << 12,
            "AP" => 1u << 13,
            "PF" => (1u << 14) | (1u << 5),
            "4K" or "K4" => 1u << 15,
            "5K" or "K5" => 1u << 16,
            "6K" or "K6" => 1u << 17,
            "7K" or "K7" => 1u << 18,
            "8K" or "K8" => 1u << 19,
            "FI" => 1u << 20,
            "RD" => 1u << 21,
            "CN" => 1u << 22,
            "TP" => 1u << 23,
            "9K" or "K9" => 1u << 24,
            "CO" => 1u << 25,
            "1K" or "K1" => 1u << 26,
            "3K" or "K3" => 1u << 27,
            "2K" or "K2" => 1u << 28,
            "V2" => 1u << 29,
            "MR" => 1u << 30,
            _ => 0,
        };

        return bits != 0;
    }
}
