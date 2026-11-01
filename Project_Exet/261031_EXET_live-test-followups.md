# Live test follow-ups EXET

## Corresponding plan and execution date

Executed 2026-10-31 under [the user-authorized PLAN](../Project_Plan/261031_PLAN_live-test-followups.md).

## Related artifacts

[TEST evidence](../Project_Test/261031_TEST_live-test-followups/README.md). Branch
`codex/live-test-followups` from `master` (`150f546`), worktree
`C:\01_Projects\Panel Cladding Full\PC-followups`. The live acceptance run that found both points
is on `codex/live-acceptance-tests` (finding 3 and 4 of its TEST record).

## Implemented scope

- `PanelCladdingExtrusionPlanningService.IsPanelBranchPath(layerPath, projectCode)`: the project's
  `Surfaces - PNL` branch with every sublayer, excluded ones included. `IsPanelLayerPath` (PCpid,
  naming sync, report) still drops excluded layers; curve routing still fails with
  `PANEL_CLADDING_PANEL_LAYER_EXCLUDED`.
- `LivePanelAutoAttributes.Refresh` uses `IsPanelBranchPath`, so sizes, area, weight and `UD_PLANE`
  of Not in Scope / By Others panels are refreshed on open, on edits and on layer renames again.
- `PanelCladdingReportWindow`: the issue columns are named; on the list's `SizeChanged` the Detail
  column takes the width Object (150) and Layer (340) leave, at least 240. At the default 1180 x 760
  window the three columns fit (before, their fixed 150 + 380 + 520 px were wider than the list).
- Package README: excluded layers keep their auto values.

## Deviations from plan

None.

## Discovered issues

None.

## Test record

- `excluded-layer-keywords`: `IsPanelBranchPath` includes `Surfaces - Not in Scope` and
  `Surfaces - By Others::WT_01`, the branch root and normal type layers, and excludes the curve
  branch, another project's branch and `Reference::Surfaces - Not in Scope`; the auto-attribute
  source calls it and no longer calls `IsPanelLayerPath`. Panel and curve scopes unchanged.
- `pcreport-command`: at 1280 x 800 the columns fill the list without running past it; at the
  default 1180 x 760 they fit.
- Targeted Debug run (`excluded-layer-keywords`, `pcreport-command`, `panel-auto-attributes`,
  `panel-cladding-layer-standard`, `panel-cladding-pid*`, `panel-cladding-naming-convention`): 9/9.
- Full regression and builds: see the TEST record.
- Not run: live acceptance in Rhino (needs a package and an attested install).
