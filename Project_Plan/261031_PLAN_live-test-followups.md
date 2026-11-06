# Live test follow-ups PLAN

## Background and authorization

The 1.3.0 live acceptance run ([TEST](../Project_Archive/Project_Test/261031_TEST_live-acceptance-tests/README.md)
on `codex/live-acceptance-tests`) found three plug-in points the user asked on 2026-10-31 to change:

- Finding 3: panels on `Not in Scope` / `By Others` layers no longer get their auto values refreshed,
  so their `UD_PLANE` and sizes go stale (and PCbake then reports an extent error before the
  excluded-layer error). The user prefers the values kept up to date: "data is better than none".
  The panels stay excluded from numbering, curves, the naming sheet and the report; only the auto
  values return.
- Finding 4: PCreport's Detail column is cut off at the default window width ("fix").
- Finding 2: PCreport lists every object with a PID outside the managed layers as Info (D07; 7,627
  rows in ABC, mostly wireframe copies on `Surfaces - GLS` / `TER` / `MP` / `STN`). The copies must
  keep their PID and stay where they are; the user asked the report to skip objects the plug-in
  does not manage instead of reporting them.

This request authorizes the construction change and its PLAN/EXET/TEST records; not packaging,
installation or a version change.

## Goals

- The auto attributes (`CW_2.00`-`CW_2.02`, `CW_2.07`, `CW_6.02`, `UD_PLANE`) are refreshed on every
  panel of the `<CODE>-IEF-Wireframe::Surfaces - PNL` branch, excluded sublayers included, on open,
  on geometry/input edits and when layers are renamed or moved.
- PCpid, PCbake curve routing, the naming-convention sync and PCreport keep excluding those panels.
- PCreport's Detail column fills the issue list's remaining width; Object and Layer keep fixed widths.
- PCreport checks only what the plug-in manages: rule D07 ("PID outside the managed layers") is
  removed, leaving 26 rules; objects off the managed layers raise nothing, PID or not.

## Architecture ownership

- Application: `PanelCladdingExtrusionPlanningService` gains `IsPanelBranchPath` (the panel branch
  with its excluded sublayers); `IsPanelLayerPath` is unchanged.
- Infrastructure: `LivePanelAutoAttributes.Refresh` uses `IsPanelBranchPath`.
- UI: `PanelCladdingReportWindow` sizes the Detail column when the list's size changes.
- Application: `PanelCladdingComplianceService` drops the D07 rule and its check.

## Affected files

`PanelCladdingExtrusionPlanningService.cs`, `LivePanelAutoAttributes.cs`,
`PanelCladdingReportWindow.xaml(.cs)`, `PanelCladdingComplianceService.cs`; smokes
`excluded-layer-keywords` (branch predicate, auto attributes source) and `pcreport-command` (column
fit; D07 fixture objects now raise nothing); package README (excluded layers, PCreport checks).

## Acceptance

- Managed: `IsPanelBranchPath` includes excluded sublayers and nothing outside the branch;
  `IsPanelLayerPath` still excludes them; the auto-attribute refresh uses the branch predicate. At
  1280 x 800 the report's three columns fit inside the issue list. The report's rule list has no
  D07; a `Surfaces - GLS` copy with a PID and a Brep on a near-miss layer raise no issue.
- Debug/Release builds and the full regression pass.
- Live (after a later install): a Not in Scope panel's auto values follow its geometry again.

## Risks and rollback

Auto values on excluded panels change on the next open of each model (one-time refresh message).
Revert the branch to restore the 1.3.0 behaviour.
