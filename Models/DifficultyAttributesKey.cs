using osu.Game.Rulesets.Mods;

namespace SosuBot.PerformanceCalculator.Models;

public record DifficultyAttributesKey(int BeatmapId, int? HitObjects, Mod[] Mods);