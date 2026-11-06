# PCreport command PLAN

## Background and authorization

On 2026-10-31 the user asked for a `PCreport` that checks whether attributes and the layer
structure are compliant, shown in a pop-up built on the existing WPF UI. Decisions from the
discussion: missing keys are not errors (Info); check panels, STEP surfaces and curves; rules are
fixed in code and reuse the plug-in's own predicates; layers named "Not in Scope" / "By Others"
are not tracked (separate change `excluded-layer-keywords`); no one-click fixes; command name
follows the PC + camel case rule. This request authorizes the construction change and its
PLAN/EXET/TEST records; not packaging, installation or UI automation.

## Goals

- `PCreport` reads the active saved model (read-only: no writes, no Undo record) and opens a
  modeless report window.
- Issues have a severity: Error (a command fails or produces wrong output), Warning
  (non-compliant, tolerated or corrected on the next write), Info (observation).
- Clicking an issue selects and zooms its object in Rhino; Rescan re-reads the model; Export
  writes an `.xlsx` beside the model.

## Architecture ownership

- Domain: report snapshot (layers, objects with kind, layer, name and user text), issue, report,
  rule metadata.
- Application: `PanelCladdingComplianceService` (pure) evaluates the snapshot using the existing
  predicates and services (`PanelCladdingExtrusionPlanningService`, `PanelCladdingSpawnPlanningService`,
  `PanelCladdingCidService`, `PanelCladdingKeyService`, `PanelCladdingPidSetupService.RequiredKeys`,
  `PanelCladdingPlaneFormat`, `PanelCladdingProjectService`). `IPanelReportNavigator` interface.
- Infrastructure/Rhino: `LivePanelCladdingReportReader` (snapshot), `LivePanelReportNavigator`
  (select and zoom). Infrastructure/File: `OpenXmlPanelCladdingReportWriter`.
- UI: `PanelCladdingReportWindow` (WPF, editor palette), `PanelCladdingReportCommand`.

## Rules

Layers: project root missing (Error); retired layer tree (Error); panel directly on
`Surfaces - PNL` (Error); layer name close to a standard name (Warning); STEP family not matching
the material (Warning); curve type layer without panel type layer (Info); not-tracked layers (Info).

Attributes, per tracked panel: key repeated in another case (Error); duplicate CID (Error);
cladding/frame data unreadable by `PanelCladdingKeyService.Parse` (Error); PID not
`PID-<project>-...` (Warning); CID not matching PID and role (Warning); name not matching CID
(Warning); unit type not C/F/L/Z (Warning); role not matching unit type (Warning); `CW_1.09`
holding an old payload (Warning); missing setup keys (Info); legacy keys (Info); role not
lowercase (Info); `UD_PLANE` missing or unreadable (Info).

Dependencies (managed STEP surfaces and curves): no owner panel (Warning); owner on a not-tracked
layer (Info); duplicate CID (Warning); `Cladding` value not its layer (Warning); curve not on its
panel's curve layer (Warning); panel with materials but no surfaces (Info); object with a PID
outside every tracked or managed layer (Info).

## Window

Header: model name, error/warning/info counts, Rescan, Export. Left: rules with issues, grouped
by Layers / Attributes / Dependencies, severity dot and count. Right: the rule's fix hint and its
issues (object, layer, detail); selecting rows selects and zooms them in Rhino.

## Affected files

New: `Domain/PanelCladdingReportModels.cs`,
`Application/Services/PanelCladding/PanelCladdingComplianceService.cs`,
`Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingReportReader.cs`,
`Infrastructure/Rhino/LivePanelReportNavigator.cs`,
`Infrastructure/File/OpenXmlPanelCladdingReportWriter.cs`, `UI/PanelCladdingReportWindow.xaml(.cs)`,
`UI/PanelCladdingReportCommand.cs`, smoke `tests/Smoke/pcreport-command`. Changed:
`Application/Interfaces` (navigator), installer command list (12 commands), package checks,
`panel-cladding-pc-commands`, package README.

## Usage

Run `PCreport` in a saved model. Fix issues with the named commands (PCpid, PCeditor save,
PCbake, re-layering), then Rescan.

## Acceptance

- Managed: each rule fires on a fixture and a compliant panel has no issue; not-tracked panels are
  skipped; export round-trips the issue rows; the window renders counts, rule groups and issues and
  calls the navigator for selected rows; command contract and the 12-command lists.
- Debug/Release builds and the full regression pass.
- Live (Rhino, not run here): PCreport on ABC/XYZ opens the window, rows select objects, export
  writes the workbook.

## Rollback

Revert the branch; the command and its files go away. Nothing in the model is changed by PCreport.

## Future extensions

Safe one-click fixes; rules configurable per project; geometry-based checks (auto values against
measured size, flipped normals).
