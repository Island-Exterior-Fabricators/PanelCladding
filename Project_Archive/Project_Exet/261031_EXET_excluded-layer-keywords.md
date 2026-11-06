# Excluded layer keywords EXET

## Corresponding plan and execution date

Executed 2026-10-31 under [the user-authorized PLAN](../Project_Plan/261031_PLAN_excluded-layer-keywords.md).

## Related artifacts

[TEST evidence](../Project_Test/261031_TEST_excluded-layer-keywords/README.md); new smoke
[`tests/Smoke/excluded-layer-keywords`](../../tests/Smoke/excluded-layer-keywords/Program.cs). Branch
`codex/excluded-layer-keywords` from `master` (`717e155`), commit `00bd7ae`, in the worktree
`C:\01_Projects\Panel Cladding Full\PC-casing`. `codex/pcreport-command` is stacked on it.
[PR 12](https://github.com/nuofan-island/PanelCladding/pull/12), opened and merged into `master` at the
user's request.

## Implemented scope

- `PanelCladdingExtrusionPlanningService.ExcludedLayerKeywords` (`Not in Scope`, `By Others`) and
  `IsExcludedLayerPath`: any segment whose letters and digits, lowercased, contain a keyword's.
- `IsPanelLayerPath` and `IsCurveLayerPath` exclude such paths, which removes them from PCpid
  numbering and context, auto attributes, the naming-convention sync, PCbake's managed-curve
  discovery and PCreport's checks without touching those adapters.
- `ResolveCurveLayerPath` fails with `PANEL_CLADDING_PANEL_LAYER_EXCLUDED`, so PCbake creates no
  curves for an excluded panel.
- Package README: a paragraph in the layer-standard section (and the section now names PCbake
  instead of spawn/update).

## Deviations from plan

None.

## Discovered issues

The package README's layer-standard section still said "spawn/update"; it was updated with this
change. Values on excluded panels are left as they are, as planned.

## Test record

- Targeted Debug run (`excluded-layer-keywords`, `panel-cladding-layer-standard`,
  `panel-cladding-pid*`, `panel-auto-attributes`, `panel-cladding-naming-convention`,
  `panel-cladding-managed-cid-scope`, `panel-cladding-extrusion-sync`,
  `panel-cladding-curve-topology`): 11/11.
- `panel-cladding-layer-standard` updated: ABC panel scope 8 layers (was 9); `Not in Scope` and
  `By Others` paths rejected; the `Not in Scope` curve-routing case removed.
- The full suite ran on `codex/pcreport-command`, which contains this commit; results are in that
  branch's TEST folder and EXET.

## Acceptance alignment

Managed criteria met. Live (pending an installed 1.3.0): PCpid does not renumber `Not in Scope`
panels; opening a model leaves their auto values alone.

## Rollback

Revert the commit; the layers are tracked again.

## Remaining limitations

- A new PID equal to one held by an excluded panel is refused by PCpid as a collision.
- Curves and surfaces already made for excluded panels stay in the model; PCreport lists them as
  belonging to an untracked owner.

## Conclusion

Layers named `Not in Scope` or `By Others` are outside every panel and curve scope of the plug-in.
