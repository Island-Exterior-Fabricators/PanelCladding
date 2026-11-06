# Panel cladding layer standard TEST

> **Client data removed on 2026-11-04.** Files this record produced from client project models (exports, screenshots, object and layer dumps) were removed from the repository and its history, and project codes and paths here were replaced with fictional ones; see the [client data cleanup EXET](../../../Project_Exet/261104_EXET_client-data-cleanup.md). The conclusions below stand; links to the removed files no longer resolve.

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-layer-standard`](../../../tests/Smoke/panel-cladding-layer-standard/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-layer-standard`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Plan: [261023_PLAN_panel-cladding-layer-standard](../../Project_Plan/261023_PLAN_panel-cladding-layer-standard.md)

Managed regression for the project layer standard:

| Objects | Layer |
| --- | --- |
| Source panels | `<PROJECT>-IEF-Wireframe::Surfaces - PNL::Surfaces - <type>` |
| Extrusion curves | `<PROJECT>-IEF-Wireframe::Curves - PNL::Curves - <type>` |
| Cladding surfaces | `Reference::Surfaces - STEP::Surfaces - <prefix>::<material>` |

`abc-layer-standard.json` is a read-only capture from the live
`ABC - Wireframe.3dm` on 2026-10-23: the complete layer list, the six observed
panel-layer → curve-layer pairs (joined by PID over 849 configured panels), and
every `Cladding` value/layer pair (STEP output and the wireframe glass copies).
It contains no geometry. The smoke asserts that the plug-in's planners reproduce
the live pairings, that panel/curve/STEP scopes select exactly 9/18/21 ABC layers,
that wireframe copies stay outside managed scope, that retired trees fail closed,
that colours/workbook categories are unchanged, and that a real ABC WT_11 panel
plans `Reference::Surfaces - STEP::Surfaces - MPL|LVR` surfaces and
`Curves - WT_11` curves. It also scans `src/` for the retired layer names.

```powershell
dotnet run --project Project_Test/261023_TEST_panel-cladding-layer-standard/PanelCladdingLayerStandardSmoke.csproj -c Debug
dotnet run --project Project_Test/261023_TEST_panel-cladding-layer-standard/PanelCladdingLayerStandardSmoke.csproj -c Release
```

No Rhino native runtime is required. Live command acceptance (PCSpawnCrv/
PCSpawnSrf/PCUpdate/PCSyncSrf/PCpid in Rhino) needs the 1.0.98 RHP installed and is
recorded separately in the EXET.

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../../../Project_Test/261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
