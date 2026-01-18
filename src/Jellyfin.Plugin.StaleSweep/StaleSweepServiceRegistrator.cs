using Jellyfin.Plugin.StaleSweep.Services;
using Jellyfin.Plugin.StaleSweep.ScheduledTasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.StaleSweep;

public sealed class StaleSweepServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<StaleSweepService>();
        serviceCollection.AddSingleton<IScheduledTask, StaleSweepTask>();
    }
}

