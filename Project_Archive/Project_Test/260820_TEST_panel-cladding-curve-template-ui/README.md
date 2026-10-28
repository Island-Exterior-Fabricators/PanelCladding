# Panel cladding curve-template and responsive-UI smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-curve-template-ui`](../../../tests/Smoke/panel-cladding-curve-template-ui/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-curve-template-ui`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

This focused smoke verifies:

- H/V priority merge-run planning and canonical mask round-tripping;
- deleted/hidden segment preservation and priority reapplication idempotence;
- `PCCrvTemplate` command registration, selection-before-priority flow, batch Undo, and rollback
  source contracts;
- complete save-button labels at the editor's constrained supported width; and
- a taller material catalogue whose content-measured tiles wrap into as many columns as fit.

Run from the repository root in both configurations:

```powershell
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-curve-template-ui\PanelCladdingCurveTemplateUiSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-curve-template-ui\PanelCladdingCurveTemplateUiSmoke.csproj -c Release
```

The smoke writes two off-screen WPF QA renders into this folder:

- `panel-cladding-footer-980x700.png`
- `material-catalogue-responsive-720x740.png`
