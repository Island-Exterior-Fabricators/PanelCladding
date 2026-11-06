# Release 1.4.0 TEST

> **Client data removed on 2026-11-04.** Files this record produced from client project models (exports, screenshots, object and layer dumps) were removed from the repository and its history, and project codes and paths here were replaced with fictional ones; see the [client data cleanup EXET](../../../Project_Exet/261104_EXET_client-data-cleanup.md). The conclusions below stand; links to the removed files no longer resolve.

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

## Publication

```powershell
git tag -a v1.4.0 8a9421b -m '...'
git -c credential.username=nuofan-island push -u origin codex/release-1.4.0 v1.4.0
gh release create v1.4.0 --verify-tag --title 'PanelCladdingEditor 1.4.0' --notes-file <RELEASE-NOTES-1.4.0.md at v1.4.0> --latest <zip> <zip>.sha256
gh release view v1.4.0 --json assets   # compare digests with the local files
```

- Release [`v1.4.0`](https://github.com/nuofan-island/PanelCladding/releases/tag/v1.4.0) by
  `nuofan-island`, published at `2026-11-01T18:18:00Z` as GitHub reports it; not a draft or
  prerelease; `releases/latest` returns `v1.4.0`.
- Assets: the zip (16 005 968 bytes, `sha256:e95b54cb...29a9352`) and its `.sha256` (105 bytes,
  `sha256:45214953...16c9df8f`); both digests equal the local files' SHA-256.
- Tag object `3c1ca09` on GitHub equals the local annotated tag and points to `8a9421b`.
- The installed and staged packages come from the same zip.

## Installation and attestation

The user ran `Install.cmd` from the staged folder with no Rhino running. From the Terminal panel
(host shell) afterwards ([`install-attestation.txt`](install-attestation.txt)):

- `PlugIn\FileName` = `%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.4.0\PanelCladdingEditor.rhp`,
  SHA-256 equal to the verified RHP; exact 13-command `CommandList` (`PCweb` included); the
  installer's `-Mode Validate`: valid (complete registration).
- After Rhino started on it: only the root and `CommandList` times advanced (06:47:42 -> 06:48:44
  UTC); `PlugIn` kept its install time. Rhino loaded `plugin\1.4.0` with `1.4.0+8a9421b`
  (live check 1.0).

## Live acceptance against the installed 1.4.0

Rhino 8.36 started from the Terminal panel; suites from `tests/Live/PcLive.cs` through MCP_Rhino.

| Suite | Model | Result |
| --- | --- | --- |
| `commands` ([json](live/commands+bake.json), first part) | XYZ LIVE TEST | 9/9: RHP `1.4.0+8a9421b` from `plugin\1.4.0`; exactly 13 commands with `PCweb`; old spellings run; removed commands unknown; no alias or toolbar calls them |
| `bake` ([json](live/bake.json)) | XYZ LIVE TEST | 18/18 |
| `excluded` ([json](live/excluded.json)) | XYZ LIVE TEST | 14/14: Not in Scope / By Others panels get their auto values refreshed on reopen and after each rename (1.4.0), are not numbered by PCpid and get no curves |
| `report` ([json](live/report.json), [window](live/report-window.png)) | XYZ LIVE TEST | 10/10; the Detail column reaches the window's right edge; rules: no D07 |
| `web` ([json](live/web.json)) | PCW copy of XYZ | 13/13 with the installed command (no `assembly`) |
| `-PCweb` through `RunScript` ([txt](live/pcweb-registered-command.txt)) | PCW copy of XYZ | registered command; unmodified model opens; modified model shows `Unsaved changes. Save before opening Wireframe Web <Save> ( Save  LastSave )`, `LastSave` opens the last save and the file is not written |
| `otherdocs` ([json](live-abc/otherdocs.json)) | ABC LIVE TEST, XYZ naming copy, new document | 5/5: no issue for the `Surfaces - GLS` copies and no D07 (1.4.0); PCreport on ABC in 0.8 s (2,062 info, 7,627 fewer than 1.3.0); the other model replaces the window; the naming sheet leaves the excluded type out; an unsaved document is refused |

The first `bake` run (in `commands+bake.json`) failed 4 checks: its panel A was the PCpid test copy
the 1.3.0 `excluded` run had left on `PNL-EXT-3A.1` (`PCLIVE_NEW_PANEL=1`, no PID), and PCbake
correctly refused it (`PANEL_CLADDING_UPDATE_PID_REQUIRED`). `PanelsOn` now skips such copies; the
rerun above passed 18/18.

Not run: `export` and `editor` (they need `Watch-Dialogs.ps1` to answer Win32 dialogs; both are
unchanged since their 1.3.0 acceptance). Closing the Terminal tab that had started the XYZ Rhino
ended that Rhino after its suites had finished; the other two were closed normally.
