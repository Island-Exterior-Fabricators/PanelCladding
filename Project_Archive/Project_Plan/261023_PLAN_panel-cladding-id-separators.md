# Panel cladding PID/CID separator PLAN

## Background and authorization

On 2026-10-23 the user asked to reverse how PIDs and CIDs use `_` and `-`
(`CID_ABC_N2_05_05-C` becomes `CID-ABC-N2-05-05_C`), to change how the plug-in
concatenates CIDs, and to convert every PID and CID in `ABC - Wireframe.3dm`.
Asked whether curve codes inside curve CIDs keep their underscore, the user chose
to swap them too and confirmed that `CRV` changes as well (`FRM_2` becomes `FRM-2`).
This explicit change authorizes the construction PLAN/EXET/TEST chain.

## Goals

- `-` separates the tokens of one identifier: `PID-ABC-N2-05-05`, `CID-ABC-N2-05-05`.
- `_` joins a panel CID to its role and components: `_P`/`_C`, cells `_0A`, curves `_FRM-0`.
- Curve codes use `-`: `FRM-0`..`FRM-3`, `INT-A0`, `INT-0A`, `INT-A`, merged `INT-A0–1`.
- Object names follow the CID: `N2-05-05_C`, `N2-05-05_C_0A`.
- Migrate ABC in place: PID, CID and `CRV` values plus CID-derived names, one Undo record.

## Architecture ownership

`PanelCladdingCidService` owns the separators, prefixes, role suffixes and the
component join; PCpid, spawn, extrusion and sync planning use it instead of string
literals. Curve codes stay with extrusion planning, live geometry partitioning, the
editor's extrusion model and the frame-assignment validator. No new commands or keys.

## Key design

- `FromPanelId` maps only `PID-` to `CID-`; retired `PID_` values are not reinterpreted.
- `ShortName` strips `CID-ABC-`, else `CID-`.
- `IncludesDependency` and PCpid's generated-dependency check use `_P_`/`_C_` and `cid_`.
- `CW_1.09_FRAME_TYPE` perimeter keys and length-modifier keys are `FRM-n`; segment
  keys (`H:0:1`) and profile codes (`1D-ALU-H0651`) are unchanged. `FRM_n` keys fail
  validation.
- Document migration is a character swap `_`↔`-` of `CW_1.01_PID`, `CW_1.02_CID` and
  `CRV` values, which maps each old ID exactly to the new rule and preserves
  uniqueness and duplicates. Names equal to the old short name become the new short
  name; other names are untouched. A read-only survey first confirms no other key,
  block definition or frame payload embeds the old forms.

## Files involved

`PanelCladdingCidService`, `PanelCladdingPidPlanningService`,
`PanelCladdingSpawnPlanningService`, `PanelCladdingExtrusionPlanningService`,
`PanelCladdingSurfaceSyncPlanningService`, `PanelFrameAssignmentService`,
`LivePanelCladdingGeometryPartitionService`, `UI/PanelCladdingExtrusion.cs`,
`UI/PanelCladdingEditorWindow.xaml.cs`; version 1.0.99 and package README; regression
literals in `Project_Test`; new `Project_Test/261023_TEST_panel-cladding-id-separators/`.

## Acceptance criteria

- The user's example and every ID kind (PID, panel CID, surface CID, curve CID, CRV,
  names, frame keys) follow the new rule; retired forms are not produced.
- ABC read-back: no old-format PID/CID/CRV values; all curve and STEP-surface CIDs
  agree with their PID and code; names agree with CIDs.
- Managed regressions and Debug/Release builds pass apart from pre-existing failures.

## Risks and rollback

Any document not migrated becomes inconsistent with 1.0.99 (new spawns would carry
new IDs beside old ones). ABC is migrated in one Rhino Undo record before saving;
reverting the branch restores the old generator. The installed plug-in must match the
document format; 1.0.98 must not be used on the migrated ABC.

## Future extensions

A reusable migration command if other projects need the same conversion.
