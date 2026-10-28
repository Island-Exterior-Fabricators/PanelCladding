# First release validation — 2026-10-14

> **Code moved on 2026-10-28.** This folder keeps the evidence only: `Test-Package.ps1`, `Test-UserInstallation.ps1`, `Verify-Release.ps1` moved to [`tests/Package`](../../../tests/Package/). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Run from the repository in a **non-elevated** PowerShell session:

```powershell
./Project_Test/261014_TEST_first-package-release/Verify-Release.ps1
```

This serializes six managed suites in Debug and Release, solution builds and final
production RHP rebuilds, verifies compiled assembly/class GUIDs and version,
builds the ZIP, checks its checksum and every payload hash, then tests the extracted
ZIP using Windows PowerShell 5.1. The older registration regression also runs.

To verify a built/downloaded ZIP separately:

```powershell
./Project_Test/261014_TEST_first-package-release/Test-Package.ps1 -Archive ./Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.97-win-x64.zip
```

## Results

- Six suites passed in **both Debug and Release** (12 runs): standalone editor,
  PID, PID layer scope, PID setup keys, point order and PID planar bounds.
- Both solution builds and direct RHP rebuilds passed with **zero warnings/errors**.
- PE metadata confirmed assembly-level GUID and plug-in class GUID both equal
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the package manifest;
  compiled assembly version is `1.0.97.0` and package version is `1.0.97`.
- ZIP checksum and all **32 manifest payload hashes** passed. The ZIP contains
  33 files including the bundle manifest. Required native dependencies exist both
  beside the RHP and in Windows x64 runtime paths. No RhinoCommon, duplicate
  PanelCladdingEditor DLL, MCP dependency, PDB or other-platform runtime is shipped.
- Windows PowerShell **5.1.26100.8115**, under a token without Administrator rights,
  passed fresh install, Validate, command repair, stale RHP-pointer repair,
  uninstall, WhatIf preservation and modified-file preservation.
- Every tested successful install/repair was followed by a separate Validate
  process. Parent-process registry reads checked the exact child RHP pointer,
  twelve commands and all three key timestamps after installer exit; reg.exe
  independently confirmed the pointer in the isolated HKCU fixture.
- HKLM input was rejected. Foreign registration survived refused repair and
  uninstall. A locked ownership manifest forced a failure after activation;
  rollback restored the prior RHP, registry and usable ownership manifest.
- The existing complete-registration smoke passed shorthand rejection,
  incomplete-command rejection and full repair.
- PowerShell parser checks and git diff whitespace checks passed.

## Findings corrected during validation

The initial solution rebuild ended with the DLL test host, so the runner now
explicitly rebuilds the production project afterward. Windows PowerShell's native
stderr handling required the test harness to check expected failures via exit code.
The product uninstaller needed the current registration ownership shape and a
read-only SHA-256 implementation compatible with PowerShell 5.1's inherited WhatIf
behavior. Refused repairs now preflight ownership before file mutations and failed
activation restores the prior HKCU export.

Logs are retained locally as ignored .log files. Fixtures use unique user-writable
folders and HKCU test keys and are removed afterward. No production installation,
Rhino process or document was changed. Native/UI acceptance was not rerun and is
not implied by these managed/package checks. Final asset hashes are supplied with
the [versioned GitHub release](https://github.com/novan-xu/PanelCladding/releases/tag/v1.0.97).
