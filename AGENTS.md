# Repository Guidelines

## Project Structure & Module Organization

- `src/Jellyfin.Plugin.StaleSweep/`: C# Jellyfin plugin source.
  - `Plugin.cs`: plugin entry point (ID, description, service registration, config page wiring).
  - `PluginConfiguration.cs`: persisted settings (libraries, age limit, dry run, TV mode).
  - `Services/StaleSweepService.cs`: scan + delete logic.
  - `ScheduledTasks/StaleSweepTask.cs`: Jellyfin scheduled task wrapper.
  - `Api/StaleSweepController.cs`: admin-only API used by the config page.
  - `Resources/`: embedded dashboard config assets (`configPage.html`, `configPage.js`).
- `Directory.Build.props`: central Jellyfin package version (`JellyfinVersion`).
- Tests: none currently (add under `tests/` if introduced).

## Build, Test, and Development Commands

- `dotnet build -c Release`: builds the plugin DLL for deployment.
- `dotnet build -c Debug`: faster local build for iterative changes.
- `dotnet test`: runs tests (only applicable once a `tests/` project exists).

## Coding Style & Naming Conventions

- Language: C# with `<Nullable>enable</Nullable>` and implicit usings enabled.
- Indentation: 4 spaces in `.cs`; 2 spaces in `.js`/`.html`.
- Naming:
  - Types: `PascalCase` (e.g., `StaleSweepService`).
  - Methods: `PascalCase`; locals: `camelCase`.
  - Files: match primary type name (`StaleSweepTask.cs`).

## Testing Guidelines

- If adding tests, prefer xUnit and place them in `tests/Jellyfin.Plugin.StaleSweep.Tests/`.
- Name test files `*Tests.cs` and mirror namespaces from `src/`.

## Commit & Pull Request Guidelines

- Git history is not present in this workspace (`.git` missing), so conventions can’t be derived.
- Recommended commits: imperative subject, scoped when helpful (e.g., `service: handle season deletion mode`).
- PRs: include a short description, Jellyfin version tested, and note whether you validated **Dry Run** vs **Delete** behavior.

## Security & Configuration Tips

- Keep **Dry Run** as the default and log every candidate deletion path/reason.
- Any admin UI/API must remain admin-only (e.g., `[Authorize(Policy = "RequiresElevation")]`).
