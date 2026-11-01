# Excluded layer keywords TEST

Evidence for [the PLAN](../../Project_Plan/261031_PLAN_excluded-layer-keywords.md) and
[the EXET](../../Project_Exet/261031_EXET_excluded-layer-keywords.md). The rerunnable check is
[`tests/Smoke/excluded-layer-keywords`](../../tests/Smoke/excluded-layer-keywords/Program.cs).

## Commands

```powershell
.\tests\Run-Regression.ps1 -Configuration Debug -Name excluded-layer-keywords,panel-cladding-layer-standard,panel-cladding-pid*,panel-auto-attributes,panel-cladding-naming-convention,panel-cladding-managed-cid-scope,panel-cladding-extrusion-sync,panel-cladding-curve-topology
.\tests\Run-Regression.ps1                                   # every smoke, Debug and Release
```

## What is checked

- `excluded-layer-keywords`: `Not in Scope` / `By Others` match in any segment regardless of case,
  spaces, `-` and `_` (`NOT_IN_SCOPE`, `Not-In-Scope`, `By-Others`, `by others`); `In Scope`,
  `Others` and normal type layers stay tracked; panel scope (PCpid's predicate and the extrusion
  planner's) and managed-curve scope skip excluded paths; curve routing fails with
  `PANEL_CLADDING_PANEL_LAYER_EXCLUDED` for an excluded panel and is unchanged otherwise.
- `panel-cladding-layer-standard`: the BKT layer list now has 8 panel-scope layers (was 9,
  `Surfaces - Not in Scope` dropped); `Surfaces - Not in Scope` and `Surfaces - By Others::WT_01`
  are rejected; the `Not in Scope` routing case was removed.

## Results

Targeted Debug run of the 11 smokes above: 11/11. The full suite ran on the stacked
`codex/pcreport-command` branch, which contains this change; see that TEST folder.
