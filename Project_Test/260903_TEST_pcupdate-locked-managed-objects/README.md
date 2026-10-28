# PCUpdate Locked Managed Objects Regression

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/pcupdate-locked-managed-objects`](../../tests/Smoke/pcupdate-locked-managed-objects/) (run `.\tests\Run-Regression.ps1 -Name pcupdate-locked-managed-objects`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

This focused regression verifies that retained and obsolete managed dependencies use RhinoCommon's
mode-bypassing replace/delete overloads. It also verifies retained object-level locked/hidden mode
preservation, rejects unlock/show workarounds, and checks the package patch version.

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pcupdate-locked-managed-objects\PCUpdateLockedManagedObjectsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcupdate-locked-managed-objects\PCUpdateLockedManagedObjectsSmoke.csproj -c Release
```
