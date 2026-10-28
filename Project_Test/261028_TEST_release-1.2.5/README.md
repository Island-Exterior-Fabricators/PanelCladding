# Release 1.2.5 TEST

Evidence for [the PLAN](../../Project_Plan/261028_PLAN_release-1.2.5.md) and
[the EXET](../../Project_Exet/261028_EXET_release-1.2.5.md). Host NOVAN-HOME, non-elevated
Windows PowerShell 5.1, 2026-10-28.

## Commands

```powershell
# On the clean release commit 9f48c11 (branch codex/release-1.2.5)
.\tests\Package\Verify-Release.ps1
[System.Diagnostics.FileVersionInfo]::GetVersionInfo('.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.2.5\Plugin\PanelCladdingEditor.rhp').ProductVersion
Get-FileHash .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.2.5-win-x64.zip -Algorithm SHA256
git tag -a v1.2.5 9f48c11 -m '...'
gh release create v1.2.5 --verify-tag --title 'PanelCladdingEditor 1.2.5' --notes-file .\Packaging\PanelCladdingEditor\RELEASE-NOTES-1.2.5.md --latest <zip> <zip>.sha256
gh release view v1.2.5 --json assets   # compare digests with the local files
```

## Results

`Verify-Release.ps1` on `9f48c11` (exit 0, 50 s):

- Six release suites (standalone editor, PID, PID layer scope, PID setup keys, point order, PID
  planar bounds) passed in Debug and Release.
- Solution and direct RHP rebuilds in both configurations: zero warnings and errors.
- Compiled assembly and plug-in class GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` in Debug and
  Release.
- ZIP checksum, all 32 payload hashes, Windows x64 dependencies and compiled identities passed.
- Extracted-ZIP install/repair/validate/uninstall in Windows PowerShell 5.1 passed.
- Registration smoke: complete install, shorthand rejection, incomplete-command rejection and full
  repair passed.
- `git status` afterwards: clean.

Package:

| Item | Value |
| --- | --- |
| RHP product version | `1.2.5+9f48c1196c51e9e13ab046c496a22851c63e7d1f` |
| RHP SHA-256 | `5CC60E2CD8510519628D93F03E4F493561E99059E216F6C36F313D913677222D` |
| `PanelCladdingEditor-1.2.5-win-x64.zip` | 15 983 705 bytes, SHA-256 `3a44c8bc152952b0e708a1a7d0ca5a58d88fd6c8a791d340c3b5ff8fa8f7e3bb`, 35 entries |
| `PanelCladdingEditor-1.2.5-win-x64.zip.sha256` | 105 bytes, SHA-256 `5ebfa79e4595f47b74228957c9fe2f442fe0ad9e9890f13cb98f50329017103e`; names the zip hash above |
| Bundled package README | contains the corrected thirteen-command text |

Publication read-back:

- Release [`v1.2.5`](https://github.com/novan-xu/PanelCladding/releases/tag/v1.2.5), published
  2026-10-28T12:28:18Z by `novan-xu`; not a draft or prerelease; `releases/latest` returns
  `v1.2.5`.
- Both asset digests reported by GitHub equal the local SHA-256 values above.
- Tag object `3dedcb0` on GitHub equals the local annotated tag and points to `9f48c11`.

An earlier run on `master` `09365d8`, before the README commit, also passed; its package was not
published because its RHP names `09365d8`.

Not run: live Rhino acceptance and production installation of 1.2.5 on this host.
