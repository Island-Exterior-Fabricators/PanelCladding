# Suspend curve extrusion assignment in PCEditor

## Background and authorization

On 2026-10-31 the user asked to temporarily remove assigning extrusion codes to
curves and keep only the layout editing in PCEditor's Extrusion view. This request
authorizes the construction work and its PLAN/EXET/TEST records.

## Goals

- Extrusion view offers layout editing only: selection, Merge/Explode/Hide,
  Add H/V, Delete, dimension edits, Undo and the existing save buttons.
- Users cannot assign, remove, clear or modify curve profile assignments, and the
  canvas does not show assigned codes on curve labels.
- The suspension is reversible: the assignment code stays in place, dormant.

## Architecture ownership

Standalone PanelCladdingEditor WPF presentation in `UI/`. No change to Application
services, Domain models, Rhino adapters, saved attribute keys, commands, the plug-in
GUID or the package. No MCP surface.

## Key design

- One private static switch in `PanelCladdingEditorWindow`, off by default, gates
  the assignment surface: the floating `ExtrusionAssignmentSection`, the
  `ExtrusionLegendSection` catalogue with its Setup button, the assigned-code suffix
  on canvas curve labels, and the FRAME CONFIGURATION note and tooltip that mention
  assigned profiles.
- With the surface hidden there is no drop target, so no assignment path is
  reachable. Handlers, catalogue loading and assignment state stay as they are.
- Saved `UD_FRAME_ASSIGNMENTS` data is untouched. Panels that already carry
  assignments keep them; layout edits adjust them by the existing rules (a merge
  unions the run's codes, a delete drops the deleted segments' codes) and saves
  write them back. PCMatchCrv, PCSpawnCrv, PCSyncCrv and PCUpdate keep their
  current behaviour with whatever assignments a panel already has.

## Files

- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml` and `.xaml.cs`.
- Smokes that drive the assignment UI turn the switch on to keep covering the
  dormant code: `tests/Smoke/pc-editor-canvas-assignment` and the real-schedule UI
  path of `tests/Smoke/panel-extrusion-assignment`. The static
  `tests/Smoke/extrusion-assignment-dropzone` contract check still holds unchanged.
- New smoke `tests/Smoke/extrusion-assignment-suspension`.
- A suspension note in `Packaging/PanelCladdingEditor/README.md`.
- `Project_Test/261031_TEST_extrusion-assignment-suspension/` and the matching EXET.

## Usage

Open PCEditor and switch to Extrusion view. Select curves and edit the layout as
before; no profile catalogue or assignment panel appears. To restore assignment,
set the switch back on and rebuild.

## Acceptance

- Default editor: Extrusion view shows the layout actions and FRAME CONFIGURATION,
  hides the catalogue, the floating assignment panel for any curve selection, and
  assigned codes on labels of a panel that already has assignments.
- Layout edits (merge, hide, delete) still work and Undo; Save Extrusions keeps a
  panel's existing assignments and records the edited topology.
- Cladding view is unchanged.
- With the switch on, the existing assignment smokes pass unchanged in behaviour.
- Debug and Release builds and the full `tests/Run-Regression.ps1` suite pass.
- No Windows UI automation, live Rhino edit, package build or installation.

## Risks and rollback

A user cannot see or change assignments on panels that already have them until the
switch is restored; their data stays intact. Merge still refuses segments whose
existing length modifiers differ, and those modifiers cannot be edited meanwhile. Rollback is reverting this change or
turning the switch back on. Live acceptance in Rhino needs a new build installed
through the usual attested channel, which this change does not do.

## Future extensions

Restore assignment, possibly as a separate mode, once the user settles the workflow.
