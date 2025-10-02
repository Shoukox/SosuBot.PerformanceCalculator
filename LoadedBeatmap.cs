using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Skinning;

namespace SosuBot.PerformanceCalculator;

public class LoadedBeatmap : WorkingBeatmap
{
    private readonly IBeatmap _beatmap;

    public LoadedBeatmap(IBeatmap beatmap) : base(beatmap.BeatmapInfo, null)
    {
        _beatmap = beatmap;
    }

    public override Stream? GetStream(string storagePath)
    {
        return null;
    }

    public override Texture? GetBackground()
    {
        return null;
    }

    protected override IBeatmap GetBeatmap()
    {
        return _beatmap;
    }

    protected override Track? GetBeatmapTrack()
    {
        return null;
    }

    protected override ISkin? GetSkin()
    {
        return null;
    }
}