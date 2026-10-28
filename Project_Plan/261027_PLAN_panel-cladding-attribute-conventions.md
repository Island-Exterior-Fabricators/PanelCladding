# Panel cladding attribute conventions PLAN

## Background and authorization

On 2026-10-27 the user asked that the plug-in follow the latest model attribute conventions, using
the live `XYZ - Wireframe.3dm` and `ABC - Wireframe.3dm` models as the reference. A read-only
comparison of both models with the plug-in found the differences below; the user then decided:

1. `UD_PLANE` replaces `Plane`. The plug-in reads only `UD_PLANE`, and `UD_PLANE` becomes a value
   the plug-in computes automatically.
2. `CW_1.09_FRAME_TYPE` is a hand-filled field (XYZ holds `oCornerL`, `iCornerR`, ...). The plug-in
   no longer reads, writes or deletes it (`PCCreate` deleted it); its only plug-in use is the
   Frame-Type table of the naming-convention sheet. The frame-assignment payload the plug-in kept
   there moves to `UD_FRAME_ASSIGNMENTS`.
3. The naming-convention sync records the `CW_1.09_FRAME_TYPE` values in the sheet's Frame-Type
   table, the way it records wall types.
4. `CW_1.12_INSTALL_SEQUENCE`, `CW_2.06_PENETRATION_DIMENSION`, `CW_7.00_*` and `CW_1.10_*` are
   hand-assigned; present or absent is normal and the plug-in does not interpret them. `PCpid` keeps
   adding blank placeholders, but never overwrites a key that already exists.
5. `PCpid` keeps the ABC identity form; levels such as `02B` stay whatever the user types.
6. Role values in `CW_5.00_DEFINITION_A` are lowercase (`parent`, `child`). The plug-in writes them
   lowercase and lowercases a stored role when it writes the panel.
7. Object names drop `CID-<project>-` (everything up to and including the second `-`), not a
   hard-coded `CID-ABC-`.
8. ABC's `Config` key is retired; the user removes it from the model. No plug-in change.

The request authorizes the PLAN -> EXET -> TEST records, the code change, Debug/Release builds and
the managed regressions. It does not authorize packaging, installation, UI automation or editing a
project model.

## Findings that shaped the design

- ABC: 929 panels carry `UD_PLANE` as object user text (`{x, y, z};{x,y,z};{x,y,z}`). In all 926
  readable values Z = X x Y points out of the facade of the panel's elevation, Y is up and X is
  right seen from outside; 40 sampled panels' front-face frames agree. The stored origins do not
  match the current geometry (stale), and the plug-in never uses the origin.
- The plug-in read `Plane` from the Brep's geometry user strings, so `UD_PLANE` (object user text)
  was never seen: frames came from the face plane or a best-fit plane.
- `CW_1.09_FRAME_TYPE` holds no plug-in payload in either model; 62 XYZ panels hold text that the
  plug-in currently rejects as `PANEL_FRAME_ASSIGNMENTS_PAYLOAD_INVALID`.
- XYZ's PNL sheet already has a Frame-Type table (row 12 title in L, values in M): `01 = NA`
  (blank frame type, as used in XYZ's PNL codes), `02`-`05` the four corner types, `06`-`28`
  placeholders. ABC's sheet has a Frame-Type header in row 7 (L7:M7) but no table yet.

## Goals

1. Frames come from `UD_PLANE` on the panel's object user text, otherwise from the geometry with
   the same convention; `Plane` is not read.
2. Every panel on `<PROJECT>-IEF-Wireframe::Surfaces - PNL` (and sublayers) carries a current
   `UD_PLANE`, refreshed with the other auto attributes.
3. No plug-in command reads, writes or deletes `CW_1.09_FRAME_TYPE` except the naming sync (read)
   and `PCpid` (blank placeholder when absent). Frame assignments persist in `UD_FRAME_ASSIGNMENTS`.
4. The PNL sheet's Frame-Type table lists the model's frame types with codes in the project format.
5. `PCpid` placeholders never overwrite; roles are lowercase; names drop `CID-<project>-`.

## Architecture ownership

- Application: `PanelCladdingKeyService` (key names), `PanelCladdingPlaneFormat` (new, pure: format,
  parse, tolerance match of `UD_PLANE`), `PanelCladdingAutoAttributeService` (desired values and
  change set incl. `UD_PLANE`), `PanelFrameAssignmentService` (payload key),
  `PanelNamingConventionService` (Frame-Type table), `PanelCladdingPidSetupService` (placeholders,
  role), `PanelCladdingCidService` (role constants and normalization, short name).
- Infrastructure/Rhino: `LivePanelPlane` (new): front normal, computed frame, stored-plane read from
  object attributes and frame orientation, shared by `LivePanelCladdingRepository` and
  `LivePanelCladdingGeometryPartitionService` (which today duplicate the frame code).
- Infrastructure/Rhino/Live: `LivePanelAutoAttributes` writes `UD_PLANE`; `LivePanelCladdingCidService`
  normalizes the role; command write paths use `UD_FRAME_ASSIGNMENTS`; `LivePanelNamingConventionSync`
  collects frame types.
- Infrastructure/File: `OpenXmlPanelNamingConventionWorkbook` reads and writes the Frame-Type table.

## Design

### UD_PLANE

- `PanelCladdingKeyService.PlaneKey = "UD_PLANE"`.
- Convention: Z = the panel's outward normal (front face normal, `OrientationIsReversed` applied,
  as `PCpid` uses); Y = world up projected onto the plane (the face plane's Y axis when the panel is
  horizontal); X = Y x Z (right seen from outside). Origin = lower-left corner of the panel's
  extent in that frame. Curved / non-planar panels use the best-fit plane of the render mesh, its
  normal turned to agree with the faces' average front normal.
- Format `{ox, oy, oz};{xx,xy,xz};{yx,yy,yz}` (the form already in ABC), invariant numbers rounded to
  6 decimals without trailing zeros. A stored value is current when it parses and its origin is
  within the document tolerance and both axes within 1e-9 (1 - dot) of the computed frame, so
  formatting differences never cause rewrites.
- Read: `LivePanelPlane.Resolve(brep, samples, tolerance, storedText)` uses the stored plane
  (oriented as today: gravity-up, X sign from the stored X) when it parses, else the computed frame.
  Callers pass the panel's object user text value; all frame users (editor descriptor, extent
  measurement, create snapshot, sync inference, spawn grid) go through it.
- Auto attribute: `UD_PLANE` joins the auto keys; editing it by hand queues a refresh. Width,
  height and dimension are measured in the computed frame. The first open of a model rewrites the
  plane of every panel whose stored origin is stale or that has none (ABC ~all, XYZ all 401); the
  open summary line names the plane too.

### Frame assignments and CW_1.09

- `PanelCladdingKeyService.FrameAssignmentsKey = "UD_FRAME_ASSIGNMENTS"`. Decode reads it (legacy
  `CW_2.09_FRAME_ASSIGNMENTS` fallback unchanged); editor save, `PCMatchCrv`, `PCCrvTemplate`,
  `PCSyncSrf`, `PCCreate`, `PCClear` and frame-key validation write/delete it instead of
  `CW_1.09_FRAME_TYPE`. A payload stored in `CW_1.09` by 1.1.0-1.2.4 is not read (none exists in
  XYZ or ABC).
- `FrameTypeKey` (`CW_1.09_FRAME_TYPE`) stays for the naming sync and the `PCpid` placeholder; the
  stale-editor fingerprint drops it (the `UD_` prefix covers the new key).

### Frame-Type table

- Usage collects the trimmed nonblank `CW_1.09` values of in-scope panels (values that start with
  `{` are leftover payloads and skipped) and whether any panel has a blank one.
- `BuildTables`: like wall types, with the project's Frame-Type format (`PanelNamingFormat.FrameType`):
  sheet rows keep their codes, a new value takes the first code without a value (placeholder rows
  first), rows nothing uses are struck through. A row whose value is `NA` counts as used while any
  panel's frame type is blank. Values without a free code are reported like uncoded wall types.
- Workbook: table title "Frame-Type", description title "Value", in the row-12 title column if
  present, else under the row-7 "Frame-Type" header, else column L. A sheet without the table and
  with nothing to record is left untouched.

### PCpid, roles and names

- `PanelCladdingPidSetupService.CreateWrites`: a required key is written only when no key of that
  name (any case) exists; existing values, including blanks, stay. The case-variant conflict check
  for placeholders is no longer needed and is removed. Unit dimensions, PID, CID, elevation, level
  and the Definition A role rule are unchanged.
- `ParentRole = "parent"`, `ChildRole = "child"`; `PanelCladdingCidService.NormalizeRole` lowercases a
  role value; `ReserveRoleDefinition` and `LivePanelCladdingCidService.Normalize` (every cladding
  write) store the lowercase role.
- `ShortName(cid)`: for a `CID-` value, drop everything up to and including the second `-`
  (`CID-XYZ-S-04-36` -> `S-04-36`, `CID-ABC-N2-05-05_C_0A` -> `N2-05-05_C_0A`).
- Version 1.2.5; package README key table.

## Affected files

`PanelCladdingKeyService.cs` (+ `FrameConfiguration` partial), new `PanelCladdingPlaneFormat.cs`,
`PanelCladdingAutoAttributeService.cs`, `PanelFrameAssignmentService.cs`, `PanelCladdingSaveService.cs`,
`PanelCladdingCreatePlanningService.cs`, `PanelCladdingCurveMatchPlanningService.cs`,
`PanelNamingConventionService.cs`, `PanelCladdingPidSetupService.cs`, `PanelCladdingCidService.cs`,
`PanelCladdingNamingConventionModels.cs`, new `LivePanelPlane.cs`, `LivePanelCladdingRepository.cs`,
`LivePanelCladdingGeometryPartitionService.cs` and its callers, `LivePanelAutoAttributes.cs`,
`LivePanelCladdingCidService.cs`, `LivePanelCladdingCurveTemplateService.cs`,
`LivePanelCladdingSurfaceSyncRepository.cs`, `LivePanelNamingConventionSync.cs`,
`OpenXmlPanelNamingConventionWorkbook.cs`, version files, package README; managed smokes that store
the payload under `CW_1.09` or assert `Parent` / `CID-ABC-` names.

## Usage

No new command. Open a model: `UD_PLANE` is refreshed with the other auto values. Commands store
extrusion assignments in `UD_FRAME_ASSIGNMENTS` and leave `CW_1.09_FRAME_TYPE` alone; saving the
main model records its frame types in the PNL sheet.

## Acceptance criteria

- Managed: key names; plane format/parse/match and change set; payload decoded from
  `UD_FRAME_ASSIGNMENTS`, `CW_1.09` text neither decoded nor deleted by `PCCreate`, editor save,
  `PCMatchCrv` or clear plans; Frame-Type table codes, placeholders, `NA` use, struck rows and the
  workbook round trip; `PCpid` placeholders do not overwrite; lowercase roles; short names.
- Debug and Release builds pass; the managed suite keeps the known failures only (smokes updated for
  the new keys are listed in the EXET).
- Live (Rhino, not run here): opening ABC/XYZ writes `UD_PLANE` with the axes shown above; XYZ
  panels with corner frame types open in `PCEditor`; the PNL sheet gains the frame types.

## Rollback

Revert the branch. Panels saved by 1.2.5 keep assignments in `UD_FRAME_ASSIGNMENTS`, which 1.2.4
ignores (it reads `CW_1.09`); `UD_PLANE` values stay and are harmless to 1.2.4.

## Future extensions

- Report panels whose front normal disagrees with their elevation (flipped surfaces) before the
  plane is refreshed.
