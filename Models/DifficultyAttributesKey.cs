using System.Collections.Immutable;
using osu.Game.Rulesets.Mods;

namespace SosuBot.PerformanceCalculator.Models;

public record DifficultyAttributesKey(int BeatmapId, int? HitObjects, Mod[] Mods) : IEquatable<Mod[]>
{
    public virtual bool Equals(Mod[]? other)
    {
        if (other == null) return false;
        return Mods.SequenceEqual(other);
    }
}