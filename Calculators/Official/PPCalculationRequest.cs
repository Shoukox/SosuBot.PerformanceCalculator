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

    public int RulesetId { get; init; }

    public CancellationToken? CancellationToken { get; init; }
}
