using JellyfinUser = Jellyfin.Database.Implementations.Entities.User;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StaleSweep.Services;

public sealed class StaleSweepService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<StaleSweepService> _logger;

    public StaleSweepService(
        ILibraryManager libraryManager,
        IUserManager userManager,
        IUserDataManager userDataManager,
        IFileSystem fileSystem,
        ILogger<StaleSweepService> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _userDataManager = userDataManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task RunAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            _logger.LogError("Stale Sweep plugin instance not available.");
            return;
        }

        var config = plugin.Configuration;
        if (config.LibraryIds.Length == 0)
        {
            _logger.LogInformation("Stale Sweep: no libraries selected; nothing to do.");
            progress.Report(100);
            return;
        }

        var ageLimitDays = Math.Max(0, config.AgeLimitDays);
        var cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(ageLimitDays));
        var dryRun = config.DryRun;

        _logger.LogInformation(
            "Stale Sweep starting. Libraries={LibraryCount} AgeLimitDays={AgeLimitDays} DryRun={DryRun} TvMode={TvMode}",
            config.LibraryIds.Length,
            ageLimitDays,
            dryRun,
            config.TvMode);

        var users = _userManager.Users.ToArray();
        var scanned = 0;
        var deleted = 0;

        foreach (var libraryId in config.LibraryIds.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var libraryItem = _libraryManager.GetItemById(libraryId);
            if (libraryItem is null)
            {
                _logger.LogWarning("Stale Sweep: library id {LibraryId} not found; skipping.", libraryId);
                continue;
            }

            _logger.LogInformation("Stale Sweep: scanning library '{LibraryName}' ({LibraryId}).", libraryItem.Name, libraryId);

            // Pull movies/episodes under the selected library folder and apply filtering in-process.
            var candidates = _libraryManager.GetItemList(new InternalItemsQuery
            {
                ParentId = libraryId,
                Recursive = true,
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode, BaseItemKind.Season },
                IsVirtualItem = false,
            });

            foreach (var item in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;

                if (item.DateCreated >= cutoff)
                {
                    continue;
                }

                switch (item)
                {
                    case Movie movie:
                        if (IsUnplayedByAllUsers(movie, users))
                        {
                            if (TryDeleteItemPath(movie, dryRun, $"Unwatched for {ageLimitDays} days", out var deleteCount))
                            {
                                deleted += deleteCount;
                            }
                        }
                        break;
                    case Episode episode:
                        if (IsUnplayedByAllUsers(episode, users))
                        {
                            if (TryDeleteItemPath(episode, dryRun, $"Unwatched for {ageLimitDays} days", out var deleteCount))
                            {
                                deleted += deleteCount;
                            }
                        }
                        break;
                    case Season season:
                        if (config.TvMode == TvDeleteMode.DeleteSeasonIfAnyUnwatched)
                        {
                            if (TryDeleteSeason(season, users, cutoff, dryRun, ageLimitDays, out var deleteCount))
                            {
                                deleted += deleteCount;
                            }
                        }
                        break;
                }

                if (scanned % 250 == 0)
                {
                    progress.Report(Math.Min(99, scanned / 250.0));
                }
            }
        }

        if (!dryRun && deleted > 0)
        {
            // Keep Jellyfin's DB in sync after filesystem deletion.
            _logger.LogInformation("Stale Sweep: deletions complete; queueing a library scan.");
            _libraryManager.QueueLibraryScan();
        }

        _logger.LogInformation("Stale Sweep finished. ScannedItems={Scanned} DeletedPaths={Deleted} DryRun={DryRun}", scanned, deleted, dryRun);
        progress.Report(100);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private bool IsUnplayedByAllUsers(BaseItem item, IReadOnlyList<JellyfinUser> users)
    {
        foreach (var user in users)
        {
            var data = _userDataManager.GetUserData(user, item);
            if (data is not null && data.Played)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryDeleteSeason(
        Season season,
        IReadOnlyList<JellyfinUser> users,
        DateTime cutoff,
        bool dryRun,
        int ageLimitDays,
        out int deletedPaths)
    {
        deletedPaths = 0;

        var episodes = _libraryManager
            .GetItemList(new InternalItemsQuery
            {
                ParentId = season.Id,
                Recursive = true,
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                IsVirtualItem = false,
            })
            .OfType<Episode>()
            .ToArray();
        if (episodes.Length == 0)
        {
            return false;
        }

        var allOld = episodes.All(e => e.DateCreated < cutoff);
        if (!allOld)
        {
            return false;
        }

        var anyUnplayed = episodes.Any(e => IsUnplayedByAllUsers(e, users));
        if (!anyUnplayed)
        {
            return false;
        }

        return TryDeleteItemPath(season, dryRun, $"Season has unwatched episodes older than {ageLimitDays} days", out deletedPaths);
    }

    private bool TryDeleteItemPath(BaseItem item, bool dryRun, string reason, out int deletedPaths)
    {
        deletedPaths = 0;

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return false;
        }

        var path = item.Path;

        if (dryRun)
        {
            _logger.LogWarning("Stale Sweep (Dry Run): would delete '{Path}' ({ItemType}) Reason={Reason}", path, item.GetType().Name, reason);
            deletedPaths = 1;
            return true;
        }

        try
        {
            if (_fileSystem.FileExists(path))
            {
                _fileSystem.DeleteFile(path);
                _logger.LogWarning("Stale Sweep: deleted file '{Path}' Reason={Reason}", path, reason);
                deletedPaths = 1;
                return true;
            }

            if (_fileSystem.DirectoryExists(path))
            {
                Directory.Delete(path, true);
                _logger.LogWarning("Stale Sweep: deleted directory '{Path}' Reason={Reason}", path, reason);
                deletedPaths = 1;
                return true;
            }

            _logger.LogWarning("Stale Sweep: path not found; skipping '{Path}' ({ItemType})", path, item.GetType().Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stale Sweep: failed deleting '{Path}' ({ItemType})", path, item.GetType().Name);
            return false;
        }
    }
}
