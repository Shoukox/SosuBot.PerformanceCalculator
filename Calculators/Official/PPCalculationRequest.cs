using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace SosuBot.PerformanceCalculator;

/// <summary>
/// Immutable input for one official osu! performance calculation.
/// </summary>
internal sealed record PPCalculationRequest
{
    public required int BeatmapId { get; init; }

    public required Stream BeatmapFile { get; init; }

    public double? Accuracy { get; init; }

    public bool Passed { get; init; } = true;

    public int? ScoreMaxCombo { get; init; }

    public Mod[] ScoreMods { get; init; } = [];

    public Dictionary<HitResult, int>? ScoreStatistics { get; init; }

    /// <summary>
    /// <c>maximum_statistics</c> of a submitted score. Together with <see cref="ScoreStatistics" /> on a passed
    /// score it switches the calculation to the osu! server path: statistics are used as they are and accuracy is
    /// computed the way osu! computes it, instead of being estimated.
    /// </summary>
    public Dictionary<HitResult, int>? MaximumStatistics { get; init; }

    /// <summary>
    /// <c>legacy_total_score</c> of a score set on osu!stable. osu! uses it to estimate slider breaks.
    /// </summary>
    public long? LegacyTotalScore { get; init; }

    public int RulesetId { get; init; }

    public CancellationToken? CancellationToken { get; init; }

    public bool IsSubmittedScore => Passed && ScoreStatistics is not null && MaximumStatistics is not null;
}
