# ZidoTVPoster

ZidoTVPoster is a C# Windows Service for Zidoo Poster Wall TV libraries. It polls the Zidoo API, counts unwatched TV episodes, and generates series and season poster images with unwatched badges.

## Safety Model

ZidoTVPoster defaults to dry-run mode. In dry-run mode it discovers libraries, plans poster updates, and renders service-owned artifacts without applying poster changes back to Zidoo.

Durable per-series artifacts are stored only under:

```text
<Series Folder>\.zido-tv-poster\
```

This keeps generated state beside the series it belongs to. If a series folder is removed from the shared source, the service-owned files under that series folder go with it. ZidoTVPoster does not keep a per-series poster cache in `ProgramData` or in the install folder.

Real poster application remains disabled until the Zidoo poster route is verified.

## Configuration

Configure the service with `appsettings.json`:

```json
{
  "Zidoo": {
    "BaseUrl": "http://192.168.0.209:9529",
    "StorageRoot": "D:\\MediaData\\Series",
    "MediaRootNames": [ "Series" ],
    "RequestTimeoutSeconds": 10
  },
  "PosterUpdates": {
    "PollIntervalSeconds": 60,
    "DryRun": true,
    "HideBadgeWhenZero": true,
    "BadgePlacement": "TopRight",
    "BadgeTextFormat": "{0} unwatched"
  }
}
```

`StorageRoot` is the folder, reachable from the PC running the service or console, that contains the TV series folders. `MediaRootNames` are the Zidoo URI folder names used to identify the series name in API paths. With the configuration above, a Zidoo URI like `/Series/Band of Brothers/Season 1/S01E01.mkv` maps to `D:\MediaData\Series\Band of Brothers`.

## Run Dry-Run Locally

```powershell
dotnet run --project src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj
```

## Debug Console

The solution also includes a one-shot console app for diagnostics. It reads `appsettings.json` using the same `Zidoo` configuration shape as the service. By default it performs local poster updates and state writes under each series folder:

```powershell
dotnet run --project src/ZidoTVPoster.Console/ZidoTVPoster.Console.csproj
```

Use `--dry-run` to only print discovery, mapping, unwatched counts, and planned poster updates without writing files:

```powershell
dotnet run --project src/ZidoTVPoster.Console/ZidoTVPoster.Console.csproj -- --dry-run --series "Band of Brothers"
```

Useful options are `--base-url`, `--storage-root`, `--media-roots`, `--series`, and `--dry-run`.

## Build And Test

```powershell
dotnet test
```

## Publish

```powershell
dotnet publish src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj -c Release -r win-x64 --self-contained false
```
