# Panel Cladding Type Code Format Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-type-code-format`](../../../tests/Smoke/panel-cladding-type-code-format/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-type-code-format`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates marker-free generated type codes, system-code normalization, fallback naming, collision
digest expansion, and unchanged canonical v4 identity semantics.

```powershell
dotnet run --project Project_Test/260820_TEST_panel-cladding-type-code-format/PanelCladdingTypeCodeFormatSmoke.csproj -c Debug
dotnet run --project Project_Test/260820_TEST_panel-cladding-type-code-format/PanelCladdingTypeCodeFormatSmoke.csproj -c Release
```
