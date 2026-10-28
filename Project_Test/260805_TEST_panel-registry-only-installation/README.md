# PanelCladdingEditor registry-only installation smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: `PanelRegistryOnlyInstallationSmoke.ps1` moved to [`tests/Package`](../../tests/Package/). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Build the 1.0.6 bundle, then run:

```powershell
powershell -ExecutionPolicy Bypass -File `
  .\Project_Test\260805_TEST_panel-registry-only-installation\PanelRegistryOnlyInstallationSmoke.ps1 `
  -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.6
```

The smoke uses isolated filesystem and HKCU test roots. It verifies migration of both historical
Package Manager names, one registry-owned active RHP, startup registration, hash validation,
rejection of a reintroduced Package Manager copy, and ownership-aware uninstall. Production Rhino
registration and user package files are not modified.
