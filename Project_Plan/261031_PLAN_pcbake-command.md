# PCbake command PLAN

## Background and authorization

On 2026-10-31 the user decided that `PCspawnSrf` and `PCspawnCrv` are no longer needed because
`PCupdate` already generates missing dependencies, and asked to:

1. Rename `PCupdate` to `PCbake` (panel attributes are baked into managed geometry; `PCsyncSrf`/
   `PCsyncCrv` go the other way).
2. Give it a command-line option for what to bake: all, surfaces or curves, defaulting to surfaces
   only.
3. Stop it from overwriting curves that already exist. Curve component assignment is suspended in
   PCeditor, so existing curves carry hand work that a rebuild would lose.

This authorizes the construction change and its PLAN/EXET/TEST records, not packaging,
installation or UI automation. It builds on `codex/command-name-casing` (same 1.3.0 release).

## Goals

- Commands: `PCbake` replaces `PCupdate`; `PCspawnSrf` and `PCspawnCrv` are removed (13 -> 11).
- `PCbake` option `Scope` = `Surfaces` (default on every run), `Curves` or `All`. Objects of a kind
  outside the scope are neither created, rebuilt nor deleted.
- Curves are never overwritten: a selected panel that already owns any managed curve keeps all of
  its curves as they are (no rebuild, no delete, no additions). A panel without curves gets them.
- Everything else `PCupdate` did stays: CID normalization, point-order standardization of the
  selected panels, duplicate-CID skipping, ambiguous legacy dependencies left alone, one Undo step.

## Architecture ownership

- Domain: `PanelCladdingBakeScope` (`Surfaces`, `Curves`, `All`); existing dependencies carry their
  owning panel object; the update result reports panels whose curves were kept.
- Application: `PanelCladdingDependencyReconciliationService` owns the scope filter and the
  curve-preservation rule (pure, unit-tested).
- Infrastructure/Rhino: `LivePanelCladdingUpdateService` reads existing dependencies first and
  prepares only what the plan can use; `LivePanelCladdingSpawnService` remains the geometry
  generator; its batch `Spawn` entry, `ILivePanelCladdingSpawnService`, `PanelCladdingSpawnResult`
  and the helpers only that entry used are removed.
- UI: `PanelCladdingBakeCommand` (former `PanelCladdingUpdateCommand`, same GUID) owns the option
  and the material-colour lookup formerly on the surface spawn command.

## Design

- Option: when nothing is preselected the selection prompt carries `Scope`; with preselected panels
  a `Bake` prompt shows `Scope` and Enter accepts. The default is `Surfaces` on every run.
  Material colours are read from the workbook only when surfaces are in scope.
- Reconciliation `CreatePlan(expected, existing, scope)`:
  - existing objects of a kind outside the scope are ignored (kept);
  - `CurvePreservedPanels(existing)` = owning panels of existing curves; their expected curves are
    dropped and their existing curves ignored; the plan lists them;
  - surfaces keep today's create/rebuild/delete behaviour.
- Live update: read existing dependencies, compute the preserved panels, prepare surfaces only when
  in scope and curves only for in-scope panels without curves, so a curve-planning error cannot
  block a surface bake. `Update(filePath, panelIds, scope)`.
- Output: `PCbake (<scope>) processed N panel(s): created/updated/deleted ...`, plus
  `Kept existing curves on N panel(s); delete their curves to bake new ones.`
- Installer command list, registration checks, package README (command list, PCbake section,
  removal of spawn instructions) and the earlier `PCMatch*`/`PCSpawn*` README leftovers.
- Internal service, interface and result names that say `Update` stay; they describe the
  dependency update and many contract smokes read them.

## Affected files

`UI/PanelCladdingUpdateCommand.cs` -> `UI/PanelCladdingBakeCommand.cs`; removed
`UI/PanelCladdingSpawnSrfCommand.cs`, `UI/PanelCladdingSpawnCrvCommand.cs`;
`Domain/PanelCladdingUpdateModels.cs`, `Domain/PanelCladdingModels.cs`,
`Application/Interfaces/IPanelCladdingServices.cs`,
`Application/Services/PanelCladding/PanelCladdingDependencyReconciliationService.cs`,
`Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingUpdateService.cs`,
`Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`; installer, package
README, registration checks; smokes that name the removed or renamed commands or call the update
service; new smoke `tests/Smoke/pcbake-command`.

## Usage

Select panels, run `PCbake`, Enter: cladding surfaces are created or rebuilt. `Scope=Curves` or
`Scope=All` also bakes curves for panels that have none. To rebake a panel's curves, delete them
first.

## Acceptance

- Managed: scope filtering (no action on the other kind, nothing of it deleted); curve preservation
  (panel with curves: no curve create/update/delete and reported; panel without: created;
  surfaces unaffected); option default `Surfaces` each run; 11 commands with the PC naming rule;
  installer and checks list `PCbake` and not the removed commands.
- Debug/Release builds and `tests/Run-Regression.ps1` pass in both configurations.
- Live (Rhino, not run here): default run touches only surfaces; `Scope=All` leaves a panel's
  existing curves unchanged.

## Rollback

Revert the branch; `PCupdate`, `PCspawnSrf` and `PCspawnCrv` return. No document data changes shape.

## Future extensions

An explicit option to rebuild existing curves, once curve component assignment returns.
