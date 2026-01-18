using Jellyfin.Plugin.StaleSweep.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StaleSweep.ScheduledTasks;

public sealed class StaleSweepTask : IScheduledTask
{
    private readonly StaleSweepService _service;
    private readonly ILogger<StaleSweepTask> _logger;

    public StaleSweepTask(StaleSweepService service, ILogger<StaleSweepTask> logger)
    {
        _service = service;
        _logger = logger;
    }

    public string Name => "Stale Sweep";

    public string Key => "StaleSweep";

    public string Description => "Deletes old + unwatched media from selected libraries (or logs in Dry Run).";

    public string Category => "Library";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromDays(1).Ticks,
        };
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        try
        {
            await _service.RunAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Stale Sweep canceled.");
            throw;
        }
    }
}
