# Panel Cladding Managed CID Scope Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-managed-cid-scope`](../../../tests/Smoke/panel-cladding-managed-cid-scope/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-managed-cid-scope`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates the shared managed-layer predicates and verifies that live spawn/sync source paths use
them for CID conflicts and geometry discovery.

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-managed-cid-scope\PanelCladdingManagedCidScopeSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-managed-cid-scope\PanelCladdingManagedCidScopeSmoke.csproj -c Release
```
