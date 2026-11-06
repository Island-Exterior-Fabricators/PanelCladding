# Panel cladding PID/CID separator TEST

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-id-separators`](../../../tests/Smoke/panel-cladding-id-separators/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-id-separators`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Plan: [261023_PLAN_panel-cladding-id-separators](../../Project_Plan/261023_PLAN_panel-cladding-id-separators.md)

| ID | Before 1.0.99 | Since 1.0.99 |
| --- | --- | --- |
| PID | `PID_ABC_N2_05_05` | `PID-ABC-N2-05-05` |
| Panel CID | `CID_ABC_N2_05_05-C` | `CID-ABC-N2-05-05_C` |
| Surface CID | `CID_ABC_N2_05_05-C-0A` | `CID-ABC-N2-05-05_C_0A` |
| Curve CID / CRV | `CID_ABC_N2_05_05-C-FRM_0` / `FRM_0` | `CID-ABC-N2-05-05_C_FRM-0` / `FRM-0` |
| Object name | `N2_05_05-C` | `N2-05-05_C` |

The smoke checks the user's example, PCpid output, ShortName, surface and curve CID
joins, role ownership (`_P_`/`_C_`), curve codes including the merged en dash range,
`CW_1.09_FRAME_TYPE` perimeter/modifier keys (`FRM-n` accepted, `FRM_n` rejected), and
scans `src/` for retired `PID_`/`CID_`/`FRM_`/`INT_` and `-P`/`-C` literals.

```powershell
dotnet run --project Project_Test/261023_TEST_panel-cladding-id-separators/PanelCladdingIdSeparatorsSmoke.csproj -c Debug
dotnet run --project Project_Test/261023_TEST_panel-cladding-id-separators/PanelCladdingIdSeparatorsSmoke.csproj -c Release
```

No Rhino native runtime is required. The ABC document migration and its read-back
are recorded in the EXET.
