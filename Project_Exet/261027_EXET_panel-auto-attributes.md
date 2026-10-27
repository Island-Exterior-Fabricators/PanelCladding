# Panel auto attributes EXET

## Corresponding plan and execution date

Executed 2026-10-27 under
[the user-authorized PLAN](../Project_Plan/261027_PLAN_panel-auto-attributes.md).

## Related artifacts

[TEST](../Project_Test/261027_TEST_panel-auto-attributes/README.md) with the managed smoke
`PanelAutoAttributesSmoke.csproj`. Source reviewed: `AutoATT.py` in the XYZ `03_scripts\AutoATT`
folder (an area watcher on `AddRhinoObject`/`ReplaceRhinoObject`/`ModifyObjectAttributes` writing
`Auto_Area`, toggled per session). Same working branch `codex/panel-cladding-ud-keys`, on top of the
uncommitted 1.2.2 `UD_` key change; no commit or PR was created (the user asked to hold off).

## Implemented scope

- `PanelCladdingKeyService`: `UnitAreaKey` (`CW_2.07_UNIT_AREA`), `PsfKey` (`CW_6.01_PSF`),
  `UnitWeightKey` (`CW_6.02_UNIT_WEIGHT`).
- New `PanelCladdingAutoAttributeService` (Application, pure): `CreateValues` (dimension, width,
  height `F5` via `FormatUnitDimension`; area `F3` + ` sqf`; weight = PSF × unrounded area, `F3`,
  or the persisted blank `" "`), `TryParsePsf` (leading number), `Changes` (stale, missing or
  case-variant keys only; a missing/blank key satisfies a blank weight), `IsInputKey`.
- `LivePanelCladdingRepository.MeasurePanelExtent`: the editor's existing render-mesh extent in
  the panel frame, now shared (no change to the editor's own read).
- New `LivePanelAutoAttributes` (Live): queues ids from `AddRhinoObject`, `ReplaceRhinoObject` and
  `ModifyObjectAttributes` (layer change, or `CW_6.01`/refreshed key change); full scan on
  `EndOpenDocument` (not merge/reference), on layer rename/reparent, and for a document already
  open at load. Ignores events during Undo and its own writes. On `RhinoApp.Idle` with no command
  running it measures each queued object in scope (`IsPanelLayerPath` with the file-name project;
  Brep/surface/extrusion; not reference or block geometry), computes the surface area
  (`AreaMassProperties`, converted with `RhinoMath.UnitScale` to feet²) and writes only the changes
  with `ModifyAttributes(quiet)`. A full scan prints one summary line when panels changed.
- `PanelCladdingEditorPlugin.OnLoad` registers it; no command and no toggle.
- `AUTO_panel` is neither read nor removed. PCpid's 30 setup keys are unchanged (`CW_2.07` is not
  added; the refresh creates it).
- Package README section (since 1.2.3) with the key table; version 1.2.3
  (`PanelCladdingEditor.csproj`, `package-manifest.json`); command list unchanged.

## Deviations from plan

None.

## Test record

- New smoke, Debug: `PASS: panel auto attribute regression` (4 `[OK]` groups), including the
  user's example values exactly.
- Full managed suite (77 projects including both new smokes, `dotnet run` per project, stdin empty,
  exit codes recorded): Debug 70/77, Release 70/77. The 7 failures are the known pre-existing ones
  with the same messages (identity probe and native runtime probe need arguments, hide-smoke
  command list, type-code-format preview, pc-curve-display-colors 1.0.73, curve-release role CID,
  surface-release write check). The suite rewrote the same 36 screenshots and added the same 2;
  all were restored/removed.
- Production project rebuilt afterwards: Debug and Release succeeded, 0 warnings, 0 errors.
  `git diff --check`: clean.

## Acceptance alignment

Managed criteria met. The live criteria (event refresh, open scan, Undo, scope, PCSyncSrf
agreement) need an installed 1.2.3 inside Rhino and were not run.

## Rollback

Remove the `LivePanelAutoAttributes.Register()` call (or revert the branch). Values already written
stay on the panels; the AutoATT script can be used again.

## Remaining limitations

- Shipped in 1.2.4, installed and attested on 2026-10-27 (see the
  [1.2.4 EXET](261027_EXET_panel-cladding-logic-authority.md)); the host had been loading 1.0.96.
  AutoATT should no longer be run. The live checks in the TEST record have not been run.
- Area is the panel's true surface area (as AutoATT computed): equal to width × height for a flat
  rectangle, larger for curved panels, smaller for panels with trimmed openings. The user confirmed
  on 2026-10-27 to keep the true surface area rather than width × height.
- Opening a model whose values are stale modifies it (Rhino will ask to save on close). Refresh
  writes are not their own Undo step, matching AutoATT; after Redo the values are refreshed again.
- PCpid measures width/height from the planar boundary; the refresh then rewrites them with the
  editor measurement. Both agree for planar panels, so no visible change is expected.
- Values refresh only while the plug-in is loaded (it loads at Rhino startup).

## Conclusion

Unit dimension, width, height, area and weight are now refreshed by the plug-in on every
`Surfaces - PNL` panel, always on, in the user's formats. 1.2.3 builds and passes the managed suite
apart from the known pre-existing failures; it is not packaged or installed.
