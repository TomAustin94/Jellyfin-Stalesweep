using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.StaleSweep;

public enum TvDeleteMode
{
    DeleteEpisodes = 0,
    DeleteSeasonIfAnyUnwatched = 1,
}

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public Guid[] LibraryIds { get; set; } = Array.Empty<Guid>();

    public int AgeLimitDays { get; set; } = 365;

    public bool DryRun { get; set; } = true;

    public TvDeleteMode TvMode { get; set; } = TvDeleteMode.DeleteEpisodes;
}

