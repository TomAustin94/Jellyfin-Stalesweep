using System.Globalization;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.StaleSweep.Api;

[ApiController]
[Route("StaleSweep")]
[Authorize(Policy = "RequiresElevation")]
public sealed class StaleSweepController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;

    public StaleSweepController(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    [HttpGet("Libraries")]
    public ActionResult<IReadOnlyList<LibraryDto>> GetLibraries()
    {
        // Expose "top-level" libraries (collection folders) for the dashboard config page.
        var libraries = _libraryManager
            .GetItemList(new InternalItemsQuery
            {
                Recursive = false,
                IncludeItemTypes = new[] { BaseItemKind.CollectionFolder },
            })
            .Select(i => new LibraryDto
            {
                Id = i.Id.ToString("N", CultureInfo.InvariantCulture),
                Name = i.Name ?? string.Empty,
            })
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(libraries);
    }
}
