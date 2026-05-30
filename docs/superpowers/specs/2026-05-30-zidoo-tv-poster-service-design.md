# Zidoo TV Poster Service Design

## Purpose

Build a C# Windows Service that updates Zidoo Poster Wall artwork for TV series and seasons so the poster itself shows the number of unwatched episodes.

The first target device is the user's Zidoo Z9X 8K at `192.168.0.209`, with SMB storage available at:

```text
\\192.168.0.209\Share\Storage
```

The service should be safe to run continuously. It should detect watched-state changes from the Zidoo API, regenerate only the posters whose counts changed, and preserve original artwork in the relevant series folder for easy maintenance.

## Existing Context

`D:\Development\MoviePosterGen` already proved the basic local artwork model for Zidoo:

```text
MovieName.nfo
MovieName-poster.jpg
```

It also contains useful C# poster rendering patterns using ImageSharp. This project should reuse the design lessons, but it should be a new service-oriented solution because this problem is continuous state synchronization rather than one-off poster generation.

The official Zidoo developer documentation exposes the control/API service on port `9529`. The relevant documented endpoints include:

```text
GET /ZidooControlCenter/getModel
GET /ZidooPoster/getCollectionList
GET /ZidooPoster/getCollection?id={id}
GET /ZidooPoster/getDetail?id={id}
GET /ZidooPoster/getFile/getPoster?id={id}&w={width}&h={height}
```

Live probing against `192.168.0.209` confirmed:

- The device responds on port `9529`.
- It reports model `Z9X 8K` and firmware `v1.3.05`.
- The SMB storage share is readable.
- TV series appear as API items with `type == 3`.
- Seasons appear as API items with `type == 4`.
- Episodes appear as API items with `type == 5`.
- Episode and media entries expose `watched`, `position`, `duration`, `lastWatchTime`, `playPoint`, and media `uri` values.

## Functional Requirements

The service must update both series posters and season posters.

Series poster behavior:

- Count all unwatched episodes across all seasons for the series.
- Render that total on the series poster.
- If the unwatched count is zero, render no badge by default.

Season poster behavior:

- Count unwatched episodes only within the season.
- Render that season count on the season poster.
- If the unwatched count is zero, render no badge by default.

Original poster storage:

- Store original poster assets inside the relevant series folder, not in a hidden global service cache.
- Never render from an already generated/badged poster.
- Keep generated posters beside the originals so a user can inspect or restore them easily.

For a series at:

```text
\\192.168.0.209\Share\Storage\Series\Band of Brothers
```

the service-owned files should be:

```text
\\192.168.0.209\Share\Storage\Series\Band of Brothers\.zido-tv-poster\
  original-series-poster.jpg
  original-season-01-poster.jpg
  generated-series-poster.jpg
  generated-season-01-poster.jpg
  poster-state.json
```

The exact season filename should use two-digit season numbers when the API provides a season number. If the API does not provide a season number, use a stable fallback based on the Zidoo season item id:

```text
original-season-zidoo-131-poster.jpg
generated-season-zidoo-131-poster.jpg
```

## Non-Goals

The first version will not:

- Replace Zidoo's watched tracking.
- Edit episode NFO files to store watched state.
- Require Plex, Jellyfin, Kodi, or TMDB credentials.
- Scrape online metadata.
- Modify movie posters.
- Rewrite or directly edit undocumented Zidoo database files unless no supported poster update route is available and the user explicitly accepts that risk.

## Architecture

Create a .NET solution with three projects:

```text
src/ZidoTVPoster.Core
src/ZidoTVPoster.Service
tests/ZidoTVPoster.Tests
```

`ZidoTVPoster.Core` contains pure business logic:

- Zidoo API client.
- API DTOs.
- TV library discovery.
- Watched-count calculation.
- SMB path mapping.
- Poster source/original management.
- Poster badge renderer.
- Poster update planner.
- State file reader/writer.

`ZidoTVPoster.Service` contains hosting concerns:

- .NET Worker Service.
- Windows Service integration.
- Configuration binding.
- Polling loop.
- Logging.
- Dry-run mode.

`ZidoTVPoster.Tests` contains focused tests for:

- API JSON parsing.
- Series and season unwatched count calculations.
- Zidoo URI to SMB path mapping.
- State comparison.
- Poster rendering output dimensions and badge/no-badge decisions.

## Configuration

Use `appsettings.json` plus environment-variable overrides.

Initial settings:

```json
{
  "Zidoo": {
    "BaseUrl": "http://192.168.0.209:9529",
    "StorageRoot": "\\\\192.168.0.209\\Share\\Storage",
    "MediaRootName": "Series",
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

`DryRun` should default to `true` until the poster application mechanism has been verified on the user's device.

## Data Flow

On each polling cycle:

1. Call `GET /ZidooPoster/getCollectionList`.
2. Filter returned items to `type == 3`.
3. For each TV series, call `GET /ZidooPoster/getCollection?id={seriesId}`.
4. Treat returned `type == 4` items as seasons.
5. For each season, call `GET /ZidooPoster/getDetail?id={seasonId}`.
6. Read episode items from the detail payload.
7. Count episodes where `watched == false`.
8. Use the first available episode media `uri` to map the Zidoo item back to its SMB series folder.
9. Load `poster-state.json` if present.
10. Compare current counts against previous counts.
11. For changed series/seasons, ensure original posters exist in `.zido-tv-poster`.
12. Render generated posters from originals.
13. Apply generated posters to Zidoo Poster Wall.
14. Save the updated state file.

## Folder Mapping

The API returns media URIs in the form:

```text
/Series/Band of Brothers/Season 1/Band of Brothers - S01E01 - Currahee Bluray-1080p.mkv
```

Map that to SMB by combining the configured storage root with the URI segments:

```text
\\192.168.0.209\Share\Storage\Series\Band of Brothers\Season 1\Band of Brothers - S01E01 - Currahee Bluray-1080p.mkv
```

The series folder is the path segment immediately after the configured media root name `Series`:

```text
\\192.168.0.209\Share\Storage\Series\Band of Brothers
```

If a URI cannot be mapped, skip that series or season and log a warning with the Zidoo id and URI.

## Original Poster Acquisition

The service needs a reliable original poster before it can render badges.

Preferred acquisition order:

1. If `.zido-tv-poster\original-*.jpg` exists, use it.
2. If a local source poster exists in the series or season folder, copy it into `.zido-tv-poster`.
3. If the Zidoo API can fetch the current poster via `GET /ZidooPoster/getFile/getPoster`, download it as the original.
4. If no original can be found, skip rendering and log a warning.

The service must not treat a previously generated poster as an original. It should identify generated posters by its own file names and by metadata in `poster-state.json`.

## Poster Rendering

Use ImageSharp for poster rendering.

Output rules:

- Preserve the original poster dimensions unless the original is unusually small.
- Render JPEG output at high quality.
- Use a compact high-contrast badge.
- Default badge placement is top-right.
- Keep margins proportional to poster size.
- Use a maximum badge width so long text does not dominate the poster.
- Render no badge when `HideBadgeWhenZero == true` and count is zero.

Default badge text:

```text
7 unwatched
```

The renderer should support an alternate compact style later, such as just:

```text
7
```

## Applying Posters To Zidoo

This is the main technical unknown and must be verified in implementation.

Preferred route:

1. Use a documented or discoverable Zidoo API route to set/update a poster for a series or season item.
2. Trigger or allow Poster Wall refresh.

Fallback route:

1. Write generated poster files to the local media folders using Zidoo-friendly names.
2. Use Zidoo settings that prefer local images.
3. Trigger metadata refresh if an API route exists.

Last-resort route:

1. Update files under `\\192.168.0.209\Share\Storage\.HomeTheater\Posters`.
2. Only do this after proving the filename/id mapping and backing up changed cache files.
3. Keep this behind an explicit opt-in setting because it depends on internal Zidoo cache behavior.

The first implementation should include an integration probe command or dry-run report that states which apply mechanism was selected for each poster.

## State File

`poster-state.json` lives in each series `.zido-tv-poster` folder.

Suggested structure:

```json
{
  "seriesId": 130,
  "seriesName": "Band of Brothers",
  "lastUpdatedUtc": "2026-05-30T12:00:00Z",
  "series": {
    "unwatchedCount": 0,
    "originalPoster": "original-series-poster.jpg",
    "generatedPoster": "generated-series-poster.jpg"
  },
  "seasons": [
    {
      "seasonId": 131,
      "seasonNumber": 1,
      "name": "Miniseries",
      "unwatchedCount": 0,
      "originalPoster": "original-season-01-poster.jpg",
      "generatedPoster": "generated-season-01-poster.jpg"
    }
  ]
}
```

State should be treated as an optimization, not the source of truth. The Zidoo API is the source of truth for watched state.

## Error Handling

API unavailable:

- Log the failure.
- Leave existing posters untouched.
- Retry on the next polling cycle.

SMB unavailable:

- Log the failure.
- Skip poster writes.
- Retry on the next polling cycle.

Missing original poster:

- Log the missing source.
- Skip that poster.
- Continue other series.

Poster render failure:

- Log the exception.
- Do not update Zidoo.
- Keep the previous generated poster.

Poster apply failure:

- Log the failure.
- Keep the generated poster and state unchanged for that item so the next cycle can retry.

Malformed API payload:

- Skip the affected item.
- Log enough id/name context to diagnose the payload.

## Testing Strategy

Unit tests:

- Parse representative `getCollectionList`, `getCollection`, and `getDetail` payloads.
- Count series unwatched episodes across multiple seasons.
- Count season unwatched episodes.
- Confirm watched episodes are excluded.
- Map `/Series/Show/Season 1/file.mkv` to the expected UNC path.
- Reject unmappable URIs without guessing.
- Confirm zero-count posters produce no badge when configured.
- Confirm non-zero-count posters produce changed image output.

Integration tests/manual probes:

- Confirm the service can call `getModel`.
- Confirm TV series discovery finds `type == 3` items.
- Confirm at least one real series maps to a writable SMB folder.
- Confirm the selected poster apply mechanism updates the Poster Wall.

Operational test:

- Mark one episode watched on the Zidoo.
- Wait one poll interval.
- Confirm the season poster count decreases.
- Confirm the series poster count decreases.
- Mark the episode unwatched if the device supports it.
- Confirm counts increase again.

## Rollout Plan

Phase 1: Discovery CLI/dry-run service

- Build API client and folder mapper.
- Print series and season unwatched counts.
- Write no poster files.

Phase 2: Local poster generation

- Create `.zido-tv-poster` folders.
- Acquire original posters.
- Generate series and season poster images.
- Do not apply them to Poster Wall by default.

Phase 3: Poster Wall application

- Verify API or local-image update path.
- Apply generated posters.
- Keep dry-run available.

Phase 4: Windows Service install

- Run continuously as a Windows Service.
- Poll and update dynamically.
- Document installation, configuration, and recovery.

## Open Questions For Implementation

The design is ready to implement, but these must be answered during implementation probes:

- Which API route, if any, can reliably set a custom poster for `type == 3` series and `type == 4` season items?
- Does Zidoo immediately refresh Poster Wall after a poster update, or must a refresh/clear-cache endpoint be called?
- Are season posters individually displayed and updateable on the Z9X 8K for all TV layouts?
- Are all user TV files under `/Series/...`, or should the service support multiple configured media roots?

