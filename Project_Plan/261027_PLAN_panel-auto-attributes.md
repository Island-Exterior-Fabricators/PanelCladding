# Panel auto attributes PLAN

## Background and authorization

On 2026-10-27 the user asked to move the AutoATT value refresh into the plug-in. Before, panels
to refresh were marked `AUTO_panel=1` and a standalone script
(`C:\Projects\XYZ\Wireframe\03_scripts\AutoATT\AutoATT.py`)
watched Rhino object events and wrote a computed area. Decisions given:

- It becomes a default plug-in function, always on (no toggle), for every panel on
  `Surfaces - PNL` and any of its sublayers, in each project.
- Keys and value formats (example 90 × 305 in panel, `CW_6.01_PSF` = 85):
  - `CW_2.00_UNIT_DIMENSION=90.00000x305.00000`
  - `CW_2.01_UNIT_WIDTH=90.00000`
  - `CW_2.02_UNIT_HEIGHT=305.00000`
  - `CW_2.07_UNIT_AREA=190.625 sqf`
  - `CW_6.02_UNIT_WEIGHT=16203.125`: `CW_6.01` times the area, number only, blank when
    `CW_6.01` is not available.

The request authorizes the PLAN -> EXET -> TEST records, the code change, Debug/Release builds and
the managed regressions. It does not authorize installation, UI automation or editing a project
model; those stay with the user.

## Goals

1. Any change to an in-scope panel (added, copied, geometry replaced, moved onto the layer branch,
   `CW_6.01` edited) refreshes the five values with no command and no `AUTO_panel` mark.
2. Opening a model refreshes all its panels once.
3. The values agree exactly with what `PCEditor`, `PCCreate` and `PCSyncSrf` write for
   `CW_2.00`–`CW_2.02`, so the refresh and their change detection never disagree.

## Architecture ownership

- Application: `PanelCladdingAutoAttributeService` (pure): formats, PSF parsing, desired values
  and the minimal change set. Key names live in `PanelCladdingKeyService`.
- Infrastructure/Rhino: `LivePanelCladdingRepository.MeasurePanelExtent` exposes the editor's
  existing width/height measurement (render mesh extents in the panel frame: stored `Plane`, planar
  face, or best-fit plane for curved panels).
- Infrastructure/Rhino/Live: `LivePanelAutoAttributes` subscribes to Rhino events, queues
  changed objects and writes on idle. `PanelCladdingEditorPlugin.OnLoad` registers it.

## Design

- Scope: `PanelCladdingExtrusionPlanningService.IsPanelLayerPath(layer, project)`, the same panel
  scope as PCpid and the naming sync: `<PROJECT>-IEF-Wireframe::Surfaces - PNL` and every
  sublayer, the project taken from the file name (any `*-IEF-Wireframe` root while unsaved).
  Brep, surface and extrusion objects only; reference and block-definition objects are skipped.
  `AUTO_panel` is neither read nor removed.
- Values: width and height in model units, `F5` (`FormatUnitDimension`); dimension
  `<width>x<height>`. Area is the panel's surface area (`AreaMassProperties`, as AutoATT computed),
  converted to square feet from the model unit system, `F3` + ` sqf`. Weight = PSF × area (area
  unrounded), `F3`, no unit. PSF is the leading number of `CW_6.01_PSF` (`85`, `85 psf`); blank,
  missing or non-numeric PSF gives a blank weight, stored as the plug-in's persisted blank `" "`.
  A key already blank stays as it is.
- Change set: only keys whose stored value differs are written; case variants of a key collapse
  to the canonical key. An unchanged panel is never modified, which also ends the event loop.
- Events: `AddRhinoObject`, `ReplaceRhinoObject`, and `ModifyObjectAttributes` when the layer or a
  relevant key changed; `EndOpenDocument` (not merge/import or reference) queues a full scan, and
  so does an already open document when the plug-in loads. Events during Undo are ignored (Undo
  restores values recorded with the old geometry); Redo is refreshed.
- Writes run on `RhinoApp.Idle` when no command is running, outside an undo record (as AutoATT
  did), with a re-entry flag so the plug-in's own writes are not re-queued. A full scan prints
  one summary line when it changed panels; event refreshes are silent.
- Version 1.2.3.

## Affected files

`PanelCladdingKeyService.cs` (key names), new `PanelCladdingAutoAttributeService.cs`,
`LivePanelCladdingRepository.cs` (`MeasurePanelExtent`), new `LivePanelAutoAttributes.cs`,
`PanelCladdingEditorPlugin.cs`, package README, version files.

## Usage

Nothing to run. Install the plug-in, open a project model; values refresh on open and after each
edit. The AutoATT script and `AUTO_panel` marks are no longer needed.

## Acceptance criteria

- Managed: the user's 90 × 305 in / PSF 85 example gives exactly the five values above; mm models
  convert the area; PSF parsing and blank weight; minimal change set (unchanged → empty, case
  variants collapse, existing blank weight kept).
- Debug and Release builds pass; managed suite matches the baseline apart from the new smoke.
- Live (Rhino, not run here): moving or scaling a panel, copying it, moving it onto the
  `Surfaces - PNL` branch and editing `CW_6.01` update the values; Undo restores the previous
  values; opening a model refreshes all panels; `PCSyncSrf` reports no grid change afterwards.

## Rollback

Revert the branch (or remove the `Register` call). Values already written stay on the panels.

## Future extensions

- An opt-out per document if a project ever needs frozen values.
