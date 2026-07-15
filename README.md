# Kapture (Dalamud Edition)

Dalamud plugin to track your loot in FFXIV. Captures drops, obtained items, and
rolls, with a roll monitor that shows who you're still waiting on.

> Updated for **Dalamud API 15 / .NET 10**. See the changelog in
> [`src/Kapture/Kapture.yaml`](src/Kapture/Kapture.yaml).

## Build

```
dotnet build -c Release src/Kapture/Kapture.csproj
```

Requires the Dalamud dev libraries at `%AppData%\XIVLauncher\addon\Hooks\dev\`
(present automatically if you run XIVLauncher). Output:

- `src/Kapture/bin/Release/Kapture/Kapture.dll` — loadable plugin
- `src/Kapture/bin/Release/Kapture/latest.zip` — distributable archive

Run the tests with `dotnet test src/Kapture.sln`.

## Install

**Dev install (sideload your own build).** In-game: `/xlsettings` → Experimental →
add `…\src\Kapture\bin\Release\Kapture\` under *Dev Plugin Locations*, then enable
Kapture in the plugin installer.

**Custom repository (self-serve).** [`repo.json`](repo.json) is a ready-made custom
plugin repository. Add its raw URL under `/xlsettings` → Experimental → *Custom
Plugin Repositories*. Note the download links point at this repo's GitHub
*Releases* — publish a release with `latest.zip` attached for them to resolve.

**Official repository.** The `.github/` release pipeline submits the plugin to
[goatcorp/DalamudPluginsD17](https://github.com/goatcorp/DalamudPluginsD17) (the
official channel) on a push to `master` whose commit message contains `[STABLE]`
or `[TEST]`. It requires a `PAT` secret and your own fork of `DalamudPluginsD17`.
