using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace SosuBot.PerformanceCalculator;

/// <summary>
/// Object counts of a playable (converted, mod-applied) beatmap: everything hit result generation and accuracy
/// need, without keeping the beatmap itself alive.
/// </summary>
internal readonly record struct PPCalculationBeatmapCounts(
    int HitObjects,
    int MaxCombo,
    int Sliders,
    int LargeTicks,
    int Fruits,
    int Droplets,
    int TinyDroplets,
    int HoldNotes)
{
    public static PPCalculationBeatmapCounts From(IBeatmap beatmap)
    {
        int sliders = 0, largeTicks = 0, fruits = 0, droplets = 0, tinyDroplets = 0, holdNotes = 0;

        foreach (var hitObject in beatmap.HitObjects)
        {
            switch (hitObject)
            {
                case Slider slider:
                    sliders++;
                    largeTicks += slider.NestedHitObjects.Count(nested => nested is SliderTick or SliderRepeat);
                    break;

                case Fruit:
                    fruits++;
                    break;

                case JuiceStream juiceStream:
                    foreach (var nested in juiceStream.NestedHitObjects)
                    {
                        switch (nested)
                        {
                            case Fruit:
                                fruits++;
                                break;
                            case TinyDroplet:
                                tinyDroplets++;
                                break;
                            case Droplet:
                                droplets++;
                                break;
                        }
                    }

                    break;

                case HoldNote:
                    holdNotes++;
                    break;
            }
        }

        return new PPCalculationBeatmapCounts(
            beatmap.HitObjects.Count,
            beatmap.GetMaxCombo(),
            sliders,
            largeTicks,
            fruits,
            droplets,
            tinyDroplets,
            holdNotes);
    }
}

/// <summary>
/// The cached result of preparing one beatmap for one ruleset and mod combination. Holds no hit objects.
/// </summary>
internal sealed record PPCalculationBeatmap(
    BeatmapInfo BeatmapInfo,
    DifficultyAttributes DifficultyAttributes,
    PPCalculationBeatmapCounts Counts,
    double CircleSize,
    double DrainRate,
    double OverallDifficulty,
    double ApproachRate,
    double SpeedChangeFactor);

internal static class PPCalculationBeatmapLoader
{
    public static PPCalculationBeatmap Load(
        IBeatmap decodedBeatmap,
        Ruleset ruleset,
        Mod[] mods,
        CancellationToken cancellationToken)
    {
        var workingBeatmap = new LoadedBeatmap(decodedBeatmap);
        IBeatmap playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods, cancellationToken);

        DifficultyAttributes difficultyAttributes = ruleset
            .CreateDifficultyCalculator(workingBeatmap)
            .Calculate(mods, cancellationToken);

        double speedChangeFactor = mods.FirstOrDefault(mod => mod is ModRateAdjust) is ModRateAdjust rateMod
            ? rateMod.SpeedChange.Value
            : 1;

        // Like the osu! score processor, performance calculators only read the ruleset and the unmodified
        // difficulty from the beatmap info; a minimal copy keeps metadata strings out of the cache.
        BeatmapInfo playableInfo = playableBeatmap.BeatmapInfo;
        var beatmapInfo = new BeatmapInfo(playableInfo.Ruleset, playableInfo.Difficulty.Clone())
        {
            OnlineID = playableInfo.OnlineID
        };

        return new PPCalculationBeatmap(
            beatmapInfo,
            difficultyAttributes,
            PPCalculationBeatmapCounts.From(playableBeatmap),
            playableBeatmap.Difficulty.CircleSize,
            playableBeatmap.Difficulty.DrainRate,
            playableBeatmap.Difficulty.OverallDifficulty,
            playableBeatmap.Difficulty.ApproachRate,
            speedChangeFactor);
    }
}
