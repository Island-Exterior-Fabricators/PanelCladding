# Command name casing EXET

## Corresponding plan and execution date

Executed 2026-10-31 under [the user-authorized PLAN](../Project_Plan/261031_PLAN_command-name-casing.md).

## Related artifacts

[TEST evidence](../Project_Test/261031_TEST_command-name-casing/README.md); rerunnable check
[`tests/Smoke/panel-cladding-pc-commands`](../tests/Smoke/panel-cladding-pc-commands/Program.cs).
Branch `codex/command-name-casing` from `master` `ad23285`, in a separate worktree
(`C:\01_Projects\Panel Cladding Full\PC-casing`) so the checkout on
`codex/extrusion-assignment-suspension` was not touched. No commit or PR was requested or created.

## Implemented scope

- Eleven `EnglishName` values renamed: `PCeditor`, `PCcreate`, `PCclear`, `PCupdate`,
  `PCmatchSrf`, `PCmatchCrv`, `PCspawnSrf`, `PCspawnCrv`, `PCsyncSrf`, `PCsyncCrv`,
  `PCcrvTemplate`. `PCpid` and `PCtemplatePNL` already followed the rule. GUIDs unchanged.
- Every former spelling in active files was replaced (65 files, 294 lines): command-line
  messages and comments in `src/`, `Get-PanelExpectedCommands` in the installer,
  `Install-Package.ps1`, `QUICKSTART.txt`, `package-manifest.json` `command`, the package and
  repository READMEs, `Test-PanelCladdingPluginRegistration.ps1`, `Test-UserInstallation.ps1`
  and the smokes that assert names or messages. The replacement matched only whole command
  tokens (`_PCSyncCrv` included, identifiers such as `PanelCladdingUpdateCommand` and lowercase
  capability folders excluded).
- Package README: one paragraph states the rule and that former spellings, aliases and toolbar
  macros still run.
- `panel-cladding-pc-commands` now also requires every name to match
  `^PC[a-z]+(?:[A-Z][a-z]+|PNL)*$` and scans `src/PanelCladdingEditor` (`.cs`, `.xaml`), the
  installer, `Install-Package.ps1`, `QUICKSTART.txt` and the manifest for former spellings.
- Version 1.3.0 (`PanelCladdingEditor.csproj`, `package-manifest.json`).

Unchanged on purpose: `Project_Plan`, `Project_Exet`, `Project_Test`, `Project_Archive`,
`RELEASE-NOTES-1.0.97.md`, `RELEASE-NOTES-1.2.5.md`, and the root README's 1.2.5 download link
(the release step updates it).

## Deviations from plan

None in behavior. The former-spelling scan excludes the package README, which names the former
spellings deliberately.

## Discovered issues

- Git Bash `sed -i` rewrote the two version files with LF endings; they were restored to the
  working tree's CRLF so the diff holds only the version lines.
- `codex/extrusion-assignment-suspension` (`a906641`, not on `master`) adds two package README
  lines with `PCEditor`, `PCMatchCrv`, `PCSpawnCrv`, `PCSyncCrv` and `PCUpdate`. Whichever branch
  merges second has to rename them; the smoke does not scan the README, so it will not catch them.

## Test record

- `tests/Run-Regression.ps1 -Configuration Debug -Name panel-cladding-pc-commands`: PASS, including
  the new rule and former-spelling checks.
- `tests/Run-Regression.ps1` (all smokes): Debug 77/77, Release 77/77, exit 0
  (`regression-Debug.json`, `regression-Release.json` in the TEST folder). `git status` afterwards
  showed no tracked change outside this edit set.
- `dotnet build .\PanelCladding.sln -m:1 --no-incremental`: Debug and Release, 0 warnings, 0 errors.
- `tests/Package/Verify-PluginAssemblyIdentity.ps1 -Configuration Release`: `[OK]` plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, one product, one distinct id.
- Release RHP `1.3.0+ad23285`, SHA-256
  `59B1F65998963862643044733993C30697891A484E18E31519C09411F9450F4D` (not packaged or installed).
- Registry probe (`registry-recreate-probe.txt`): the installer's `New-Item -Force` recreates
  `CommandList`, so an upgraded registration holds only the new spellings; its validation compares
  case-insensitively.
- `git grep` for former spellings outside the historical records returns only the package README's
  deliberate mention.

Not run: `Test-PanelCladdingPluginRegistration.ps1`, `Test-UserInstallation.ps1`,
`Test-Package.ps1` and `Verify-YakPackage.ps1` need a built bundle; packaging is not part of this
change.

## Acceptance alignment

- Names, GUIDs and the package command are asserted by the smoke: met.
- No active source, installer or test file writes a former spelling: met (README mention is
  intentional).
- Installer expected list and package checks use the new names: edited; their bundle-based runs
  are pending with packaging.
- Debug/Release builds and both regression configurations pass: met.
- Live: in Rhino, the command line shows the new names and old spellings still run. Needs an
  installed 1.3.0; not run.

## Rollback

Revert the branch. Old and new spellings resolve to the same commands, so documents, aliases and
macros need no change.

## Remaining limitations

- Not packaged, installed or attested; 1.2.5 stays installed.
- Typing completion in Rhino offers the new spellings only after 1.3.0 is installed and loaded.
- The compliance report (`PCreport`) and the excluded-layer keywords follow in the same release.

## Conclusion

All 13 commands follow `PC` + camel case, the installer, package metadata, documentation and
checks agree, and the managed suite passes in Debug and Release. Packaging, installation and live
Rhino acceptance remain for the release.
