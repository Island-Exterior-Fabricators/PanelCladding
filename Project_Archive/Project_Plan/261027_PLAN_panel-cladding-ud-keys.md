# Panel cladding UD_ user-defined keys PLAN

## Background and authorization

On 2026-10-27 the user asked: the `CW_9.00_XXX` series used now becomes `UD_XXX`; make sure
the plug-in accommodates the change. The plug-in owns two keys in that series (since 1.1.0):

| Before | After | Content |
|---|---|---|
| `CW_9.01_CLADDING_LOGIC` | `UD_CLADDING_LOGIC` | cell ownership JSON |
| `CW_9.02_FRAME_CONFIG` | `UD_FRAME_CONFIG` | readable frame configuration |

The request authorizes the PLAN -> EXET -> TEST records, the code change, Debug/Release builds
and the managed regressions. It does not authorize installation, UI automation or rewriting a
project model; those stay with the user.

## Goals

1. New writes use `UD_CLADDING_LOGIC` and `UD_FRAME_CONFIG`.
2. Panels and template curves still carrying `CW_9.01`/`CW_9.02` (ABC, XYZ and any model written
   by 1.1.0–1.2.1) keep reading correctly, with no `PANEL_FRAME_CONFIG_BACKUP_MISSING` errors.
3. A legacy key is renamed on the panel's next plug-in write, in the same undoable change, so
   models migrate as they are worked on; no key ends up stored under both names.

## Architecture ownership

- Application: `PanelCladdingKeyService` owns the key names, the legacy aliases, the fallback
  read and the pure rename plan.
- Infrastructure/Rhino/Live: `LivePanelCladdingCidService.Normalize` (runs on every panel write)
  applies the rename; readers that use Rhino attributes directly fall back to the legacy key.

## Design

- `CladdingLogicKey = "UD_CLADDING_LOGIC"`, `FrameConfigBackupKey = "UD_FRAME_CONFIG"`;
  `LegacyUserDefinedCladdingLogicKey = "CW_9.01_CLADDING_LOGIC"`,
  `LegacyUserDefinedFrameConfigKey = "CW_9.02_FRAME_CONFIG"`.
- `ReadUserDefinedValue(userText, key)`: the current key when nonblank, otherwise its legacy key
  (case-insensitive). `ReadFrameAttribute` uses the same fallback, so decode, canonical read and
  name validation accept legacy panels.
- `PlanUserDefinedKeyMigration(userText)`: pure plan of deletes/writes. A legacy key is deleted;
  its value is written to the `UD_` key only when that key is absent or blank (a `UD_` value
  written by the same operation wins).
- Recognition: `IsTopologyKey` includes the legacy frame-config key and
  `IsClearableCladdingAssignmentKey` the legacy cladding-logic key, so clear, create, match,
  sync and template paths remove them when they rewrite those values. `TopologyPersistenceMatches`
  treats a stored legacy key as out of date, so `PCSyncSrf` rewrites (and migrates) such panels.
- Live: `LivePanelCladdingCidService.Normalize` applies the migration before frame-config coding;
  `LivePanelFrameConfigCodes` seeding, `LivePanelNamingConventionSync`, the curve template
  "configured" check, the editor save result and `PCSyncSrf` logic read use the fallback.
- Stale-editor fingerprint includes `UD_` keys (and keeps `CW_9.` for legacy panels).
- Version 1.2.2; package README key table updated.

## Affected files

`PanelCladdingKeyService.cs`, `PanelCladdingKeyService.FrameConfiguration.cs`,
`PanelCladdingSurfaceSyncPlanningService.cs`, `PanelCladdingMatchPlanningService.cs`,
`PanelCladdingSaveService.cs`, `LivePanelCladdingCidService.cs`, `LivePanelFrameConfigCodes.cs`,
`LivePanelNamingConventionSync.cs`, `LivePanelCladdingCurveTemplateService.cs`,
`LivePanelCladdingRepository.cs`, comments that name the keys, the naming-convention smoke's key
assertion, `PanelCladdingEditor.csproj`, `package-manifest.json`, package README.

## Usage

No new command. Open a legacy model and use the plug-in as before; each panel written by a
cladding command (`PCEditor` save, `PCCreate`, `PCMatchSrf`/`PCMatchCrv`, `PCSyncSrf`,
`PCCrvTemplate`, `PCSpawn*`/`PCUpdate` source panels) comes back with `UD_` keys.

## Acceptance criteria

- Managed: constants are the `UD_` names; a legacy-only panel decodes, reads its canonical
  configuration and validates; the migration plan renames, drops a legacy key when the `UD_` key
  is set, and is empty for a migrated panel; legacy keys are topology/clearable keys and fail
  `TopologyPersistenceMatches`; unrelated `UD_` keys are untouched.
- Debug and Release builds pass; the managed suite matches the 1.2.1 baseline (68/75, same known
  failures).
- Live (Rhino, not run here): a legacy panel opens in `PCEditor` with its layout and, after save,
  has `UD_` keys and no `CW_9.01`/`CW_9.02`.

## Rollback

Revert the branch. 1.2.1 reads only `CW_9.0x`: panels already rewritten with `UD_` keys would need
the keys renamed back (a reverse rename with the same plan), so roll back before broad use.

## Future extensions

- An explicit one-pass command or script to migrate a whole model at once, if the team wants
  models fully renamed before handing them on.
