# Repository owner transfer TEST

Evidence for [the PLAN](../../Project_Plan/261028_PLAN_repository-owner-transfer.md) and
[the EXET](../../Project_Exet/261028_EXET_repository-owner-transfer.md). Host NYCZBF16G9NXU,
non-elevated Windows PowerShell 5.1, 2026-10-28, branch `codex/repository-owner-transfer` from
`master` `ad23285` with the link edits uncommitted.

## Commands

```powershell
gh api repos/nuofan-island/PanelCladding --jq '{full_name, visibility, default_branch, permissions}'
gh api repos/novan-xu/PanelCladding --jq '.full_name'          # old name resolves to the new one
gh release list --repo nuofan-island/PanelCladding
git remote set-url origin https://github.com/nuofan-island/PanelCladding.git
git fetch origin --prune
git grep -n -i novan-xu -- . ':(exclude)Project_Exet' ':(exclude)Project_Archive' ':(exclude)Project_Test' ':(exclude)Project_Plan'
.\tests\Run-Regression.ps1
$out = '<scratchpad>\owner-transfer-build'
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1 -OutputRoot $out   # build only, no -Source
.\tests\Package\Test-Package.ps1 -Archive "$out\PanelCladdingEditor-1.2.5-win-x64.zip"
.\tests\Package\Verify-YakPackage.ps1 -PackagePath "$out\panelcladdingeditor-1.2.5-rh8_33-win.yak"
Select-String README.md, QUICKSTART.txt, manifest.yml -Pattern 'github\.com'   # in the built bundle and Yak stage
git status --short
```

## Results

GitHub after the transfer was accepted:

- `nuofan-island/PanelCladding`: public, default branch `master`; `novan-xu` keeps admin, maintain,
  push, triage and pull permissions.
- `repos/novan-xu/PanelCladding` and `repos/novan-xu/MCP_Rhino` resolve to
  `nuofan-island/PanelCladding` and `nuofan-island/MCP_Rhino`.
- Releases `v1.2.5` (latest, 2026-10-28T12:28:18Z) and `v1.0.97` are listed under the new owner.

Local repository:

- `origin` fetch and push URLs are `https://github.com/nuofan-island/PanelCladding.git`; the fetch
  succeeded and brought the remote branches and tag `v1.2.5`.
- `git grep novan-xu` outside the PLAN / EXET / TEST and archive folders: no match (exit 1).

Regression (`Run-Regression.ps1`, exit 0, 606 s): Debug 77/77 and Release 77/77 passed.

Package (built into the scratchpad, not installed or published):

- Zip and Yak build succeeded in 12 s. Compiled assembly identity `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`
  in Release. The RHP product version is `1.2.5+ad232855023a8731fb37334f0dbe71398b8fc087`
  (the uncommitted working tree on `ad23285`); this local build is evidence only.
- `Test-Package.ps1`: ZIP checksum, all 32 payload hashes, Windows x64 dependencies, compiled
  identities, and the isolated extracted-ZIP install/repair/validate/uninstall in Windows
  PowerShell 5.1 passed.
- `Verify-YakPackage.ps1`: all Yak package checks passed (contents, manifest name, version, GUID
  keyword and platform, packaged RHP GUID, temporary folder-source publish and search, immutable
  republish, refusal of the public server).
- Links in the built output:
  - `README.md:475` names `https://github.com/nuofan-island/PanelCladding/releases/latest`.
  - `QUICKSTART.txt:44` names `https://github.com/nuofan-island/PanelCladding/releases`.
  - Yak `manifest.yml:7` has `url: https://github.com/nuofan-island/PanelCladding`.
- `git status` afterwards: only the four edited files and the new records.

A first Yak build attempt run as `& Build-PanelCladdingEditorYak.ps1 *>&1` stopped at Yak's
"Content version doesn't match manifest" warning: in Windows PowerShell 5.1 the all-stream
redirect turned that stderr line into a terminating error under the script's
`$ErrorActionPreference = 'Stop'`. The rerun without the redirect is the result above.

Not run: live Rhino acceptance and production installation (the change does not affect plug-in
behaviour).
