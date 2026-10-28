# Panel cladding attribute conventions EXET

## Corresponding plan and execution date

Executed 2026-10-27 under
[the user-authorized PLAN](../Project_Plan/261027_PLAN_panel-cladding-attribute-conventions.md).

## Related artifacts

[TEST](../Project_Test/261027_TEST_panel-cladding-attribute-conventions/README.md) with the managed
smoke `PanelCladdingAttributeConventionsSmoke.csproj`. Branch
`codex/panel-cladding-attribute-conventions` from `master` (`340515f`); no commit or PR was created.
The reference models (`XYZ - Wireframe.3dm`, `ABC - Wireframe.3dm`) were only read, through the
live Rhino connection; neither was modified.

## Implemented scope

- `PanelCladdingKeyService`: `FrameAssignmentsKey = "UD_FRAME_ASSIGNMENTS"`, `PlaneKey = "UD_PLANE"`;
  `FrameTypeKey` (`CW_1.09_FRAME_TYPE`) documented as hand-filled. `IsClearableCladdingAssignmentKey`
  and `ValidateFrameAttributeNames` use `UD_FRAME_ASSIGNMENTS` (legacy `CW_2.09` unchanged).
- Frame assignments: `PanelFrameAssignmentService.Decode` reads `UD_FRAME_ASSIGNMENTS`; editor save,
  `PCCreate`, `PCMatchCrv`, `PCCrvTemplate` and `PCSyncSrf` write/delete it instead of `CW_1.09`.
  No plug-in path reads, writes or deletes `CW_1.09_FRAME_TYPE` except the naming sync (read) and
  the `PCpid` placeholder (only when missing). The stale-editor fingerprint no longer lists `CW_1.09`.
- `UD_PLANE`:
  - New `PanelCladdingPlaneFormat` (Application): format `{ox, oy, oz};{xx,xy,xz};{yx,yy,yz}`
    (6 decimals, no trailing zeros or negative zero), parse (first nine numbers, unit axes) and
    tolerance match (origin within the document tolerance, axes within 1e-9 of 1 - dot).
    `PanelPlaneFrame` added to the domain models.
  - New `LivePanelPlane` (Infrastructure/Rhino): stored value from object user text; `Resolve`
    (stored `UD_PLANE`, oriented as before, else computed); `Compute` (front-face normal with
    `OrientationIsReversed`, as `PCpid`; best-fit plane for non-planar panels with its normal turned
    to the faces' front; Y = world up in the plane; X = Y x Z; origin = lower-left extent corner);
    `Orient` (the former `OrientFrame`). The duplicated frame code in `LivePanelCladdingRepository`
    and `LivePanelCladdingGeometryPartitionService` (and the geometry `Plane` reads) was removed;
    the editor layout, match snapshot, create snapshot, sync inference and spawn grid pass the
    panel's stored `UD_PLANE`.
  - Auto attribute: `UD_PLANE` joins `PanelCladdingAutoAttributeService.Keys` (and so the input
    keys); `CreateValues` overload with the frame; `Changes` keeps a stored plane that matches.
    `LivePanelCladdingRepository.MeasurePanel` (replaces `MeasurePanelExtent`) measures width and
    height in the computed frame and returns it; `LivePanelAutoAttributes` writes it and names
    `UD_PLANE` in the open summary.
- Frame-Type table: `PanelNamingConventionUsage.FrameTypes`/`HasBlankFrameType`,
  `PanelNamingConventionSheetContent.FrameTypes`, `PanelNamingConventionTables.FrameTypes`/
  `UncodedFrameTypes`. `BuildTables` codes frame types with the wall-type algorithm (now a shared
  `CodeValues` helper; wall-type behavior unchanged) in the project's Frame-Type format; a row whose
  value is `NA` is used while any panel's frame type is blank. The workbook reads/writes a
  `Frame-Type` / `Value` table (row-12 title, else under the row-7 header, else L); an untitled
  table is not read, is skipped while it has no rows and is checked for overlap once it has rows.
  The sync collects nonblank `CW_1.09` values (skipping `{`-prefixed leftover payloads), reports
  uncoded frame types and counts them in its summary.
- `PCpid`: a setup key that exists (any case, blank or not) is never written; the case-variant
  conflict failure for placeholders is gone. The CID check reads the stored text plus the writes.
  `ReserveRoleDefinition` stores lowercase roles and skips a definition key that already holds its
  value.
- Roles: `ParentRole = "parent"`, `ChildRole = "child"`, `PanelCladdingCidService.NormalizeRole`;
  `LivePanelCladdingCidService.Normalize` (every cladding write) lowercases a stored role.
- `ShortName`: a `CID-` value loses everything up to and including the second `-`.
- Version 1.2.5 (`PanelCladdingEditor.csproj`, `package-manifest.json`); package README key table,
  auto-attribute table, naming-sync paragraph, assignment and object-name paragraphs updated.

## Deviations from plan

- `ReserveRoleDefinition` also skips unchanged existing Definition keys (the PLAN named only the
  placeholders). Without it `PCpid` rewrote Definition B-D with their own values, canonicalizing a
  lower-case key name, which contradicts "never overwrite a key that already exists".
- Older smokes updated for the new rules (no production code changed after their first failing
  run apart from the bullet above):
  - payload key `FrameTypeKey` -> `FrameAssignmentsKey`: panel-cladding-clear, create-command,
    topology-persistence, curve-topology, panel-extrusion-assignment (its source-contract check
    and message now name `UD_FRAME_ASSIGNMENTS`);
  - short names: object-names (`CID-OTHER-S1-06-14` -> `S1-06-14`), id-separators
    (`CID-QRS-N1-01-01` -> `N1-01-01`), pc-editor-canvas-assignment (its project-less fixture
    `PID-N1-03-07` / `CID-N1-03-07_C` gained the `ABC` segment so the heading stays `N1-03-07_C`);
  - pid-setup-keys: existing keys are untouched, roles lowercase, case variants left alone;
  - naming-convention: role expectations lowercase (an unchanged existing Definition B is no longer
    written); the PCtemplatePNL sync checks on the XYZ fixture give the model XYZ's four frame types
    and a blank one, since the fixture's Frame-Type table holds them and would otherwise be struck.
  Their historical README results are not rewritten.

## Discovered issues

- The plug-in read `Plane` from the Brep's geometry user strings; ABC keeps its plane as object
  user text, so even before the rename no stored plane was used. 1.2.5 reads object user text.
- ABC's stored `UD_PLANE` origins do not match the current geometry (all axes do); the first open
  with 1.2.5 rewrites them.
- By the short-name rule a CID without a project segment (`CID-N1-03-07_C`) loses its first segment
  (`03-07_C`); every CID `PCpid` writes has the project segment.
- ABC data (reported to the user, not changed): 18 L01 `Surfaces - WT_04` pairs share a PID/CID (the
  99.125" piece carries the 180" piece's attributes, including `CW_2.03_OFFSET_H0=152.20455`);
  panel `f75a147d-60ea-4872-afa1-c393e9b6cccc` is named `E1-PF-07_C` but has `PID-ABC-N1-PF-02`, L /
  Parent and a CID without `_P`.
- Wireframe Web still reads `CW_9.00`/`CW_9.01`/`CW_9.02`; a separate task was proposed.

## Test record

- New smoke, Debug and Release: `PASS: panel cladding attribute conventions regression`
  (9 `[OK]` groups).
- Full managed suite (79 projects incl. the new one, `dotnet run` per project, stdin empty, exit codes
  recorded):
  - Debug, first run on the production code: 67/79 — the 7 known failures plus object-names,
    pid-setup-keys, pc-editor-canvas-assignment, id-separators and naming-convention. After the
    smoke updates and the `ReserveRoleDefinition` change above, those five passed individually.
  - Debug, full rerun: 72/79. Release, full run: 72/79. The 7 failures are the known pre-existing
    ones with the same messages: `PluginAssemblyIdentityProbe` and `NativeRuntimeProbe` (need
    arguments), hide-smoke-command (command type set), type-code-format (editor preview shape),
    pc-curve-display-colors ("must ship as package 1.0.73"), curve-release ("Role CID must remain
    unchanged"), surface-release ("Surface release-only changes must not trigger other writes").
  - The runs rewrote 36 screenshots and added 2; all were restored/removed (`git status` shows no
    PNG changes).
- Production project rebuilt afterwards with `--no-incremental`: Debug and Release succeeded,
  0 warnings, 0 errors. `git diff --check`: clean.

## Acceptance alignment

Managed criteria met. The live criteria (open ABC/XYZ and inspect `UD_PLANE`, `PCEditor` on a XYZ
corner panel, `PCCreate` keeping `CW_1.09`, the XYZ PNL Frame-Type table, `PCpid` on hand-filled
keys) need an installed 1.2.5 inside Rhino and were not run.

## Rollback

Revert the branch. Panels saved by 1.2.5 keep assignments in `UD_FRAME_ASSIGNMENTS`, which 1.2.4
ignores; `UD_PLANE` values and lowercase roles are harmless to 1.2.4.

## Remaining limitations

- Not packaged, installed or attested; 1.2.4 stays installed. Packaging must re-verify the compiled
  RHP GUID metadata (AGENTS.md).
- Opening a model with 1.2.5 rewrites `UD_PLANE` on nearly every panel once (stale ABC origins, none
  in XYZ), outside an Undo record like the other auto values; the model is marked modified.
- A panel whose front face points inward gets a `UD_PLANE` (and so a frame) facing inward; 40
  sampled ABC panels and all 926 stored ABC planes agree with their elevation.
- Assignments that 1.1.0-1.2.4 stored in `CW_1.09_FRAME_TYPE` are no longer read (none exist in XYZ
  or ABC).
- Roles are lowercased only when a command writes the panel (or `PCpid` runs); ABC's 1008 `Parent`/
  `Child` values change gradually. ABC's `Config` key is left for the user to delete.

## Conclusion

1.2.5 reads the panel frame only from `UD_PLANE` and keeps it current as an auto value, moves
frame assignments to `UD_FRAME_ASSIGNMENTS` and leaves `CW_1.09_FRAME_TYPE` to the user (recording
it in the PNL Frame-Type table), never overwrites existing `PCpid` keys, writes lowercase roles and
names objects without `CID-<project>-`. It builds and passes the managed suite apart from the known
pre-existing failures; it is not packaged or installed.
