# Release 1.4.0 TEST

Evidence for [the PLAN](../../Project_Plan/261101_PLAN_release-1.4.0.md) and
[the EXET](../../Project_Exet/261101_EXET_release-1.4.0.md). Host NOVAN-HOME, non-elevated
Windows PowerShell 5.1, 2026-11-01.

## Commands

```powershell
# On the clean release commit 8a9421b (branch codex/release-1.4.0)
.\tests\Package\Verify-Release.ps1
.\tests\Run-Regression.ps1
git status --short
[System.Diagnostics.FileVersionInfo]::GetVersionInfo('.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.4.0\Plugin\PanelCladdingEditor.rhp').ProductVersion
Get-FileHash .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.4.0-win-x64.zip -Algorithm SHA256
# Stage: extract the verified zip to C:\01_Projects\Panel Cladding Full and compare every file's
# SHA-256 with the verified bundle folder.
```

## Results

`Verify-Release.ps1` on `8a9421b`: exit 0 ([`verify-release.log`](verify-release.log)).

- Six release suites (standalone editor, PID, PID layer scope, PID setup keys, point order, PID
  planar bounds) passed in Debug and Release.
- Solution and direct RHP rebuilds in both configurations: zero warnings and errors.
- Compiled assembly and plug-in class GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` in Debug and
  Release.
- ZIP checksum, all 32 payload hashes, Windows x64 dependencies and compiled identities passed.
- Extracted-ZIP install/repair/validate/uninstall in Windows PowerShell 5.1 passed (isolated roots).
- Registration smoke: complete install, shorthand rejection, incomplete-command rejection and full
  repair passed.

`Run-Regression.ps1` on the same commit: Debug 82/82, Release 82/82
([`regression-Debug.json`](regression-Debug.json), [`regression-Release.json`](regression-Release.json)).
`git status` afterwards: clean.

Package:

| Item | Value |
| --- | --- |
| RHP product version | `1.4.0+8a9421bc505eba39d727e95edde40298291d8bce` |
| RHP SHA-256 | `F93BE778A9CD15B353E0EBA6B80ED66B84E490ECF06A86E93195D9E4F0D81208` |
| `PanelCladdingEditor-1.4.0-win-x64.zip` | 16 005 968 bytes, SHA-256 `e95b54cbc197972aded30f7eec8f99667f819f19bc578f8e7661fe71c29a9352`, 35 entries |
| `PanelCladdingEditor-1.4.0-win-x64.zip.sha256` | 105 bytes; names the zip hash above |
| Bundled installer | expected commands include `PCweb` (13) |
| Bundled package README | lists `_PCweb`; thirteen-command `CommandList` |

Staged for installation: `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.4.0`, extracted
from the verified zip; 33 files, every SHA-256 equal to the verified bundle folder, `Install.cmd` at
the top, RHP `1.4.0+8a9421b`.

Not run yet: production installation on this host and the live suites against the installed
1.4.0 (they follow the user's `Install.cmd`).
