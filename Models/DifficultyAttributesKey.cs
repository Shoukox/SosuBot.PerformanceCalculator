using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;

namespace SosuBot.PerformanceCalculator.Models;

public record DifficultyAttributesKey(int BeatmapId, int? HitObjects, Mod[] Mods)
{
    public virtual bool Equals(DifficultyAttributesKey? other)
    {
        if (other == null) return false;

        var a = BeatmapId == other.BeatmapId
                && HitObjects == other.HitObjects
                && Mods.OrderBy(m => m.Acronym).SequenceEqual(other.Mods.OrderBy(m => m.Acronym));

        return BeatmapId == other.BeatmapId
               && HitObjects == other.HitObjects
               && Mods.OrderBy(m => m.Acronym).SequenceEqual(other.Mods.OrderBy(m => m.Acronym));
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BeatmapId);
        hash.Add(HitObjects);

        foreach (var mod in Mods) hash.Add(mod.Acronym);

        foreach (var setting in Mods.GetOrderedSettingsSourceProperties()) hash.Add(setting);

        return hash.ToHashCode();
    }
}