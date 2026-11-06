# Panel cladding logic authority PLAN

## Background and authorization

On 2026-10-27 the user asked to change how panels load and how `PCCreate` treats cladding:

- `PCCreate` writes the H/V offsets but also fills blank `CW_4.xx_CLADDING_*` placeholders and an
  all-blank `UD_CLADDING_LOGIC`, and deletes any `CW_x.xx_CLADDING_*` keys already on the panel.
- A panel's load (the editor "boot" and every command that parses a panel) should depend only on
  the H/V offsets while the cladding is unconfigured, and on H/V, `UD_CLADDING_LOGIC` and the frame
  configuration once it is configured.
- The `CW_4.xx` series becomes output only: the plug-in writes it but no longer reads it.
- While the cladding is unconfigured, the plug-in must not generate or overwrite the user's
  cladding information.

Decisions given in answer to two questions:

1. Cell materials move into `UD_CLADDING_LOGIC`, which becomes the single read source. Panels whose
   logic predates this read `CW_4` once on load; the next write stores the new form.
2. When `PCCreate` re-runs on a configured panel and the grid no longer fits the stored logic, it
   deletes the stale `UD_CLADDING_LOGIC` only; `PCCreate` never writes or deletes `CW_4` keys.

The request authorizes the PLAN -> EXET -> TEST records, the code change, Debug/Release builds and
the managed regressions. It does not authorize installation, UI automation or editing a project
model.

## Goals

1. Unconfigured panel (no `UD_CLADDING_LOGIC`, nor legacy `CW_9.01`): loads from H/V alone with
   blank cells; `CW_4` keys, valid or not, never affect it.
2. Configured panel: `UD_CLADDING_LOGIC` must name exactly the cells of the H/V grid, and the stored
   frame configuration must fit H/V (as today); a mismatch fails with a clear message.
3. `UD_CLADDING_LOGIC` carries materials: owner cells hold the material code, member cells their
   owner label, blank cells `""` (the same value rules as `CW_4`), for example
   `{"0A":"AL-01","0B":"0A","1A":""}`.
4. `PCCreate` writes offsets, unit dimensions, PID/CID and frame configuration only; it removes the
   logic only when it no longer fits the new grid.
5. No command creates cladding keys on an unconfigured panel as a side effect.

## Architecture ownership

- Application: `PanelCladdingLogicService` owns the logic format (encode with materials, decode of
  values and of the ownership graph). `PanelCladdingKeyService` owns "configured" detection, the
  cell values read from logic (with the one-time legacy `CW_4` read) and the grid check in `Parse`.
- Planners (`PCCreate`, `PCMatchSrf`, `PCSyncSrf`/`PCSyncCrv`, editor save) decide what they write.
- Infrastructure/Rhino/Live: the sync repository applies the plan's cladding writes only when the
  plan carries them.

## Design

- `PanelCladdingLogicService.Encode`: a value that is a cell label stays a parent reference; any
  other nonblank value is stored trimmed as the cell's material. `DecodeValues` returns
  label -> value; `Decode` keeps returning the ownership graph (materials read as roots), so
  `ResolveOwnerLabel` and graph validation are unchanged.
- `PanelCladdingKeyService.IsCladdingConfigured(userText)`: nonblank `UD_CLADDING_LOGIC` (or
  legacy `CW_9.01` via `ReadUserDefinedValue`).
- `ReadCladdingCellValues(userText)`: `CW_4` key name -> value from the logic. Legacy logic (no
  materials at all) takes each root cell's material from its own `CW_4` key once; nothing else
  reads `CW_4`. Unconfigured -> empty.
- `Parse`: cells take their values from `ReadCladdingCellValues`; when configured, the decoded
  labels must equal the grid labels, else `PANEL_CLADDING_LOGIC_GRID_MISMATCH` (naming the logic and
  grid sizes, and `PCCreate`/editor save as the way to refresh). `PanelCladdingKeySet` gains
  `CladdingConfigured`.
- `PCCreate`: no `CW_4` or logic writes; deletes only offset keys, topology/retired frame keys, unit
  keys, signatures, retired cladding type keys and retired `CW_2.13_CLADDING_LOGIC`. Stored logic
  that does not decode to exactly the new grid's labels is deleted (`UD_` and `CW_9.01`).
- `PCMatchSrf`: an unconfigured source fails as `PANEL_CLADDING_MATCH_SOURCE_NOT_CONFIGURED`. The
  "explicit source `CW_4` cell" transfer is replaced by the logic, which already holds every cell.
- `PCSyncCrv`: on an unconfigured panel the plan carries no cladding; the repository then neither
  deletes `CW_4` keys nor writes cells or logic. `PCSyncSrf` (surfaces scope) configures from
  cladding surfaces as today. The curves-scope grid rebuild reads cell values from the logic.
- Editor save: "Save Extrusions" alone is refused when it changes the H/V count of a configured
  panel (the logic would no longer fit): `PANEL_CLADDING_LOGIC_REQUIRES_SAVE_BOTH`.
- Version 1.2.4; package README key notes updated.

## Affected files

`PanelCladdingLogicService.cs`, `PanelCladdingKeyService.cs` (+ `FrameConfiguration` partial),
`PanelCladdingModels.cs` (`PanelCladdingKeySet`, sync plan/write), `PanelCladdingCreatePlanningService.cs`,
`PanelCladdingMatchPlanningService.cs`, `PanelCladdingSurfaceSyncPlanningService.cs`,
`PanelCladdingSurfaceSyncService.cs`, `LivePanelCladdingSurfaceSyncRepository.cs`,
`PanelCladdingSaveService.cs`, version files, package README; managed smokes whose fixtures put
cladding in `CW_4` move it into `UD_CLADDING_LOGIC`.

## Usage

No new command. `PCCreate` a panel, open it in `PCEditor` (blank cladding), assign materials and
save cladding: the panel is now configured. Re-running `PCCreate` keeps the cladding when the grid
size is unchanged and clears the stale logic when it changed.

## Acceptance criteria

- Managed: `PCCreate` plan has no `CW_4`/logic writes and keeps existing `CW_4` keys; it deletes
  the logic only on a grid change. Unconfigured panels parse with blank cells whatever `CW_4` holds
  (extra, missing, invalid references). Configured panels parse materials from the logic and fail
  on a label mismatch. Legacy logic reads `CW_4` owner materials. Encode/decode round trip with
  materials. `PCMatchSrf` refuses an unconfigured source. `PCSyncCrv` plans no cladding on an
  unconfigured panel. Save Extrusions with a grid-count change on a configured panel is refused.
- Debug and Release builds pass; the managed suite keeps the baseline apart from smokes updated
  for the new rule (listed in the EXET).
- Live (Rhino, not run here): the flow in Usage.

## Rollback

Revert the branch. Panels saved by 1.2.4 carry materials in `UD_CLADDING_LOGIC`; 1.2.3 rejects such
logic in `PCSyncSrf` (`PANEL_CLADDING_LOGIC_PARENT_INVALID`) but still loads from `CW_4`, which
1.2.4 keeps writing.

## Future extensions

- A model-wide pass that rewrites legacy logic with materials without opening each panel.
