# Release 1.4.0 package

Execution date: 2026-11-01 (America/New_York).

## Background and goals

1.3.0 was built from `150f546`, installed on NOVAN-HOME and accepted live
([TEST](../Project_Test/261031_TEST_live-acceptance-tests/README.md)). Its findings led to
[PR 15](https://github.com/nuofan-island/PanelCladding/pull/15): auto values on Not in Scope / By
Others panels, PCreport's Detail column, and PCreport skipping objects the plug-in does not manage.
A 1.3.1 release was prepared on 2026-10-31 (`codex/release-1.3.1`, local commit `df86842`, never
verified or pushed) and paused when the user asked for `PCweb`
([PR 16](https://github.com/nuofan-island/PanelCladding/pull/16)). On 2026-11-01 the user chose to
release both as 1.4.0. Goals: version 1.4.0, release notes, a package verified on its committed
source, and a staged bundle for the user's `Install.cmd`. Product name, existing commands and GUID
7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35 are unchanged; `PCweb` is the 13th command.

## Ownership and design

- Branch `codex/release-1.4.0` from `master` (`97bb599`) carries the 1.3.1 preparation (`df86842`)
  with 1.3.1 replaced by 1.4.0; `codex/release-1.3.1` is superseded.
- Follow the 1.2.5 procedure: commit the version and notes, then run
  `tests/Package/Verify-Release.ps1` on that clean commit (the RHP's product version embeds it:
  `1.4.0+<commit>`). Its archive is the package. Run the full regression on the same commit.
- `src/PanelCladdingEditor/PanelCladdingEditor.csproj` and
  `Packaging/PanelCladdingEditor/package-manifest.json`: 1.4.0.
- `Packaging/PanelCladdingEditor/RELEASE-NOTES-1.4.0.md` (new): install, upgrade notes from 1.2.5,
  changes in 1.3.0 and 1.4.0 (PCweb and the corrections), validation. The package README marks the
  corrections as 1.4.0.
- `tests/Live`: the loaded-RHP check takes the expected version from the plug-in itself (or
  `Parameters.version`), the Not in Scope check expects refreshed auto values, and `otherdocs`
  expects no issue for unmanaged objects, so the live suites can accept this package; `commands`
  expects 13 commands and `web` runs against the installed PCweb when no `assembly` is given.
- Stage the verified bundle in `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.4.0` for
  the user to run `Install.cmd` (the session's registry writes are redirected; installs are run by
  the user, then attested from the Terminal panel).
- Not in scope: tag, GitHub release, README download link, Yak package, merging. They follow only
  when the user asks.

## Affected files

csproj, package manifest, package README, `RELEASE-NOTES-1.4.0.md`, `tests/Live/PcLive.cs`,
`tests/Live/README.md`, PLAN / EXET / TEST records.

## Acceptance

- `Verify-Release.ps1` passes on the committed source with no tracked file changed; the full
  regression passes in Debug and Release.
- The packaged RHP reports `1.4.0+<commit>` of the verified commit and the plug-in GUID; the
  bundle's installer registers 13 commands.
- The staged bundle equals the verified archive.
- Live acceptance (commands, PCweb installed, Not in Scope, PCreport) happens after the user
  installs it.

## Rollback

Delete the branch, the generated package and the staged folder; 1.3.0 stays installed.
