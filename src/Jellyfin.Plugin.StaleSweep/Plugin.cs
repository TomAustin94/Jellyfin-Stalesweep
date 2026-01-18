using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.StaleSweep;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Stale Sweep";

    public override string Description =>
        "Deletes old + unwatched media from selected Jellyfin libraries, with a Dry Run mode.";

    public override Guid Id => Guid.Parse("6e4050cf-1ce9-4f9d-aed1-cc36cbe5d2ef");

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;
        if (string.IsNullOrWhiteSpace(ns))
        {
            yield break;
        }

        yield return new PluginPageInfo
        {
            Name = "configPage",
            EmbeddedResourcePath = $"{ns}.Resources.configPage.html",
        };

        yield return new PluginPageInfo
        {
            Name = "configPage.js",
            EmbeddedResourcePath = $"{ns}.Resources.configPage.js",
        };
    }
}
