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
    "StorageRoot": "\\\\192.168.0.209\\Share\\Storage",
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

`StorageRoot` is the SMB path that contains the configured media roots. With the configuration above, TV series are discovered under `\\192.168.0.209\Share\Storage\Series`.

## Run Dry-Run Locally

```powershell
dotnet run --project src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj
```

## Build And Test

```powershell
dotnet test
```

## Publish

```powershell
dotnet publish src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj -c Release -r win-x64 --self-contained false
```
