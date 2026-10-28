# Material Setup Color Picker Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/material-setup-color-picker`](../../tests/Smoke/material-setup-color-picker/) (run `.\tests\Run-Regression.ps1 -Name material-setup-color-picker`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

This focused WPF smoke verifies the custom-color swatch button without opening Rhino's modal picker
or using Windows UI automation. Injected picker fakes cover both confirmed and cancelled results.

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260818_TEST_material-setup-color-picker\MaterialSetupColorPickerSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_material-setup-color-picker\MaterialSetupColorPickerSmoke.csproj -c Release
```

The smoke verifies accessible button semantics and visual states, initial-color routing, confirmed
preview/hex/effective-state synchronization, palette deselection, cancellation preservation, and a
deterministic 720x740 off-screen render.
