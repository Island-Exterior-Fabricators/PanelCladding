# Panel Cladding Sparse Topology Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-sparse-topology`](../../../tests/Smoke/panel-cladding-sparse-topology/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-sparse-topology`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Focused executable regression for sparse, independent `2.10`-`2.12` mask persistence and the
merge-mask-absence precondition of `PCCrvTemplate`.

Run with:

```powershell
dotnet run --project Project_Test/260820_TEST_panel-cladding-sparse-topology/PanelCladdingSparseTopologySmoke.csproj -c Debug
dotnet run --project Project_Test/260820_TEST_panel-cladding-sparse-topology/PanelCladdingSparseTopologySmoke.csproj -c Release
```
