namespace SosuBot.PerformanceCalculator;

/// <summary>
/// Stateless performance calculation input.
/// </summary>
public sealed record PerformanceRequest
{
    public required GameMode Mode { get; init; }

    /// <summary>
    /// Legacy osu! mod bitmask accepted by rosu-pp (the value must fit in UInt32).
    /// </summary>
    public ulong Mods { get; init; }

    /// <summary>
    /// Accuracy scenario. Use this without exact hit counts.
    /// </summary>
    public double? Accuracy { get; init; }

    public uint? Combo { get; init; }

    public uint Misses { get; init; }

    /// <summary>
    /// Exact score-state fields. The relevant fields must be complete for the selected mode.
    /// </summary>
    public uint? Count300 { get; init; }

    public uint? Count100 { get; init; }

    public uint? Count50 { get; init; }

    public uint? CountGeki { get; init; }

    public uint? CountKatu { get; init; }

    public uint? PassedObjects { get; init; }

    public double? ClockRate { get; init; }

    /// <summary>
    /// Must be explicit at the native boundary because lazer and stable use different semantics.
    /// </summary>
    public bool IsLazer { get; init; } = true;

    /// <summary>
    /// Lazer slider accuracy fields. They are only relevant to osu!standard.
    /// </summary>
    public uint? LargeTickHits { get; init; }

    public uint? SmallTickHits { get; init; }

    public uint? SliderEndHits { get; init; }

    /// <summary>
    /// Optional legacy total score used by stable osu!standard calculations.
    /// </summary>
    public uint? LegacyTotalScore { get; init; }
}
