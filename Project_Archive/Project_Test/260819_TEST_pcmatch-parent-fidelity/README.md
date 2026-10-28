# PCMatch Parent Fidelity Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/pcmatch-parent-fidelity`](../../../tests/Smoke/pcmatch-parent-fidelity/) (run `.\tests\Run-Regression.ps1 -Name pcmatch-parent-fidelity`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Verifies exact transfer of the reported 2x2 parent-cell configuration, a valid non-canonical parent
direction, and the Rhino `ObjectAttributes` application path.

```powershell
dotnet run --project Project_Test/260819_TEST_pcmatch-parent-fidelity/PanelCladdingMatchParentFidelitySmoke.csproj -c Debug
```

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../../../Project_Test/261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
