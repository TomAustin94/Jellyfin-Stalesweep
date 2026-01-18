# Stale Sweep (Jellyfin Plugin)

Deletes *old + unwatched* media from selected Jellyfin libraries, with a safe **Dry Run** mode.

This repository was generated from the spec in `requirements.txt`.

## What it does

- Lets admins pick libraries to monitor.
- Scheduled task scans for items older than an age threshold (default 365 days).
- Deletes candidates directly from disk (or logs only in Dry Run).
- Triggers a library scan after deletions to keep Jellyfin in sync.

## Safety notes

- Default is **Dry Run**.
- “Unwatched” is treated as **unplayed by every user** (safer than a single-user definition).
- Every candidate deletion is logged with a reason and path.

## Build (outside this sandbox)

This workspace doesn’t include the .NET SDK, but the plugin code is set up as a standard Jellyfin plugin project.

- Build with: `dotnet build -c Release`
- Output assembly: `src/Jellyfin.Plugin.StaleSweep/bin/Release/<tfm>/Jellyfin.Plugin.StaleSweep.dll`
- Install by copying the DLL into Jellyfin’s `plugins/` folder, then restart Jellyfin.

`JellyfinVersion` is set to `10.11.5` in `Directory.Build.props` to match your server.
