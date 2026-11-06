# Panel cladding project layer standard PLAN

## Background and authorization

On 2026-10-23 the user asked to rework the plug-in to the latest layer standard,
using the already re-layered `ABC - Wireframe.3dm` as the example. Tracked panels
remain `Surfaces - PNL`, now under `ABC-IEF-Wireframe`, where `ABC` is the project
code and varies by project. Extrusion curves are hosted under `Curves - PNL`, and
STEP cladding surfaces under `Reference::Surfaces - STEP`. This explicit change
authorizes the construction PLAN/EXET/TEST chain.

## Document survey (read-only, live ABC route)

| Role | Retired layer tree | New layer tree in ABC |
| --- | --- | --- |
| Source panels | `01_CW Panels::Surfaces-PNL::<type>` | `ABC-IEF-Wireframe::Surfaces - PNL::Surfaces - WT_01` (and WT_02/03/04/07/08 (SB)/11, Not in Scope) |
| Extrusion curves | `02_CW Extrusions::Curves-PNL::<type>` | `ABC-IEF-Wireframe::Curves - PNL::Curves - WT_01` (one per panel type with curves) |
| Cladding surfaces | `04_STEP Surfaces::Surfaces-Glass::GL01` (legacy `03_Material Surfaces (STEP)`) | `Reference::Surfaces - STEP::Surfaces - GLS::GLS-001` (GLS, LVR, MPL, STN, TER) |

Evidence: all 849 PIDs whose panels carry frame configuration and CRV curves pair
exactly one `Surfaces - <type>` layer with the matching `Curves - <type>` layer.
Every STEP Brep's `Cladding` value equals its leaf layer, and its family layer is
`Surfaces - ` plus the code's leading letters. The wireframe also keeps glass
surfaces carrying the same kind of PID/CID/`Cladding` metadata under
`ABC-IEF-Wireframe::Surfaces - GLS::GLS-00x(.n)`; those are not plug-in output. `Curves - PNL` additionally holds user-owned
`Curves - Mullion Wrap` and `Curves - Snow Guards` sublayers (currently empty).

## Goals

- Recognize source panels on any `<PROJECT>-IEF-Wireframe::Surfaces - PNL` subtree.
- Spawn/update curves on `<same root>::Curves - PNL::Curves - <type>`.
- Spawn/update/sync cladding on `Reference::Surfaces - STEP::Surfaces - <prefix>::<material>`.
- Restrict managed dependency discovery to those two output trees, keeping the
  wireframe glass copies out of CID conflict and sync scope.
- Keep material default colours and workbook categories unchanged.

## Architecture ownership

Application planning services own the layer contract; Rhino adapters only call
their predicates. `PanelCladdingExtrusionPlanningService` owns the project root and
the panel/curve branches (it already owned both former roots).
`PanelCladdingSpawnPlanningService` owns the STEP root and material layer naming.
`PanelCladdingPidPlanningService` reuses the panel predicate. No new commands,
user-text keys, MCP references or registration changes.

## Key design

- Project root: a top-level layer whose name ends with `-IEF-Wireframe`
  (case-insensitive) with a non-blank project code before it. Taken from the panel's
  own layer path, never from configuration, so any project code works.
- Panel scope: root, then exactly `Surfaces - PNL`, then any depth. Similar names
  (`Surfaces - PNL-Old`), other branches and nested copies are excluded.
- Curve routing: replace `Surfaces - PNL` with `Curves - PNL`, and the
  `Surfaces - ` prefix of each type segment with `Curves - `. Unprefixed segments are
  preserved. A panel directly on `Surfaces - PNL` still fails with
  `PANEL_CLADDING_PANEL_TYPE_LAYER_REQUIRED`. Unsupported layers report the pattern.
- Managed curves: `<PROJECT>-IEF-Wireframe::Curves - PNL` subtree. Managed surfaces:
  `Reference::Surfaces - STEP` subtree. Both remain combined with geometry class and
  PID/CID checks, so user curves without plug-in metadata are unaffected.
- Material layers: family = `Surfaces - ` + uppercase leading letters of the code
  (`GLS-001` → `Surfaces - GLS`, `MTL-05.1-A` → `Surfaces - MTL`); codes without
  letters use `Surfaces - Other`. Semantic categories (Glass/Metal/Stone/...) move to
  `ResolveMaterialCategory` and continue to drive default colours and workbook
  descriptions. Existing layer-colour synchronization is unchanged.
- PCSyncSrf accepts exactly `Reference::Surfaces - STEP::<family>::<material>` and
  validates the family against the material code.
- The retired `01_CW Panels`, `02_CW Extrusions`, `04_STEP Surfaces` and
  `03_Material Surfaces (STEP)` trees are no longer recognized.

## Files involved

- `Application/Services/PanelCladding/PanelCladdingExtrusionPlanningService.cs`
- `Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- `Application/Services/PanelCladding/PanelCladdingPidPlanningService.cs`
- `Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `Infrastructure/File/OpenXmlPanelCladdingWorkbookRepository.cs`
- `Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingPidService.cs`, `UI/PanelCladdingPidCommand.cs` (messages)
- Product version 1.0.98 (`PanelCladdingEditor.csproj`, `package-manifest.json`) and package README.
- New `Project_Test/261023_TEST_panel-cladding-layer-standard/`; existing managed
  regression fixtures that hard-code the retired layer tree are moved to the new one.

## Usage

No workflow change. Keep panels on `<PROJECT>-IEF-Wireframe::Surfaces - PNL::Surfaces - <type>`.
PCSpawnCrv/PCUpdate create `Curves - <type>` beside them when missing; PCSpawnSrf/
PCUpdate create `Reference::Surfaces - STEP::Surfaces - <prefix>::<material>`.

## Acceptance criteria

- The six live ABC panel→curve layer pairs and fifteen STEP material layers are
  reproduced exactly by the planners; wireframe glass copies are outside managed scope.
- Panel, curve and STEP scopes in the ABC layer list are exactly the 9, 18 and 21
  expected layers; other project codes behave identically.
- Retired layer paths fail closed with the expected pattern in the message.
- Material colours and workbook categories are unchanged.
- No plug-in source references the retired layer names.
- Updated managed regressions, Debug/Release solution and standalone RHP builds pass.
- Packaging/installation are not part of this change; the installed 1.0.97 RHP is left in place.

## Risks and rollback

Documents still on the retired tree are no longer recognized: PCpid finds no panels,
curve spawning fails with `PANEL_CLADDING_PANEL_LAYER_NOT_SUPPORTED`, and old-tree
dependencies are not discovered. Those documents must be re-layered first (ABC
already is). `Surfaces - Not in Scope` remains part of the panel subtree, matching
the existing whole-subtree rule. Roll back by reverting the branch; no document data
is migrated by this change.

## Future extensions

An explicit exclusion list for panel sublayers (for example `Surfaces - Not in Scope`)
or a migration command for documents on the retired tree, if requested.
