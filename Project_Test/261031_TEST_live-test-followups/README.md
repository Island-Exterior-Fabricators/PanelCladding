# Live test follow-ups TEST

Evidence for [the PLAN](../../Project_Plan/261031_PLAN_live-test-followups.md) and
[the EXET](../../Project_Exet/261031_EXET_live-test-followups.md): auto attributes on Not in Scope /
By Others panels, PCreport's Detail column, and PCreport no longer reporting objects the plug-in does
not manage (rule D07 removed).

## Commands

```powershell
dotnet build .\PanelCladding.sln -c Debug -m:1
dotnet build .\PanelCladding.sln -c Release -m:1
.\tests\Run-Regression.ps1 -Configuration Debug -Name excluded-layer-keywords,pcreport-command,panel-auto-attributes,panel-cladding-layer-standard,panel-cladding-pid*,panel-cladding-naming-convention
.\tests\Run-Regression.ps1                                   # every smoke, Debug and Release
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
```

## Results (2026-10-31, NOVAN-HOME)

| Run | Result |
| --- | --- |
| Production RHP build, Debug and Release | 0 warnings, 0 errors |
| Targeted Debug smokes (list above) | 9/9 passed; after the D07 removal `pcreport-command`, `panel-cladding-layer-standard`, `excluded-layer-keywords` 3/3 |
| `Run-Regression.ps1` Debug | 81/81 passed ([`regression-Debug.json`](regression-Debug.json)) |
| `Run-Regression.ps1` Release | 81/81 passed ([`regression-Release.json`](regression-Release.json)) |
| `Verify-PluginAssemblyIdentity.ps1` Debug and Release | `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, one distinct id |

## What the changed smokes check

- `excluded-layer-keywords`: the panel scope used by PCpid, the naming sync and the report still skips
  excluded layers; `IsPanelBranchPath` covers the whole `Surfaces - PNL` branch including
  `Surfaces - Not in Scope` and `Surfaces - By Others::WT_01`, and nothing outside it; the auto
  attributes call `IsPanelBranchPath`; curve routing for an excluded panel still fails with
  `PANEL_CLADDING_PANEL_LAYER_EXCLUDED`.
- `pcreport-command`: at 1280 x 800 the Object, Layer and Detail columns fill the issue list without
  running past it, Detail at least 240 px; at the default 1180 x 760 they fit. All 26 rules fire on
  the fixture; its `Surfaces - GLS` copy and its Brep on `Surfaces-PNL`, both with a PID, raise no
  issue (they were the two D07 cases); export writes 28 issue rows.

## Renders

- `pcreport-window-1180x760.png`: default window size; the Detail text is fully visible.
- `pcreport-window-1280x800.png`: the smoke's existing render size.

## Not run

Live acceptance in Rhino: it needs a package and an attested install. With one, rerun
`tests/Live` suite `excluded` (on `codex/live-acceptance-tests`) after changing its expectation:
the Not in Scope panel's auto values must now be refreshed on reopen.
