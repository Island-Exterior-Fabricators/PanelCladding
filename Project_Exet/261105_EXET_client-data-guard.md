# Client data guard EXET

## Corresponding plan and execution date

[PLAN](../Project_Plan/261105_PLAN_client-data-guard.md),
[TEST evidence](../Project_Test/261105_TEST_client-data-guard/README.md).
Executed 2026-11-05 on host NOVAN-HOME, branch `codex/client-data-guard` from `master` at
`a8883d5` (PR 19).

## Changes

- `.githooks/Test-RepositoryContent.ps1` with modes Staged, Message, Push, Tracked and Commit;
  `.githooks/content-rules.json` with the file types and path rules moved out of the smoke plus raw
  live-output folders and new evidence images; hooks `pre-commit`, `commit-msg` and `pre-push`;
  `.gitattributes` keeps the hooks LF.
- `tests/Run-Regression.ps1` sets `core.hooksPath = .githooks` when unset (done for this clone).
- Private client list created outside the repository at `%APPDATA%\PanelCladding\content-denylist.txt`
  (7 entries: the two client projects' names, codes and numbers from the cleanup, with the metal
  material category left allowed).
- `tests/Live/PcLive.cs`: default `evidenceDir` under `%LOCALAPPDATA%\PanelCladding\LiveEvidence`, a
  folder inside a git working tree is refused, `Start` returns the folder; README, helper usage lines
  and `Show-Results.py` follow.
- `.gitignore` ignores `results*/` and `live*/` under `Project_Test/` and `Project_Archive/`.
- `repository-content-guard` smoke rewritten: shared-rule self-test, hook wiring, Tracked scan,
  ignore check, scratch-repository hook behaviour.
- `AGENTS.md`, the plan log and `tests/README.md`: rules, private list, no new images in TEST folders.

## Deviations from plan

None in scope. Two findings during execution changed the implementation:

- Windows PowerShell writes a byte order mark ahead of a redirected standard input, so the first id
  sent to `git cat-file --batch` was reported missing. The script now sends a leading blank line and
  fails (exit 2) if any requested object is missing instead of skipping it.
- A file the Claude session writes under `%APPDATA%` is not visible on the host (the same redirection
  as its registry writes). The list was written into the project folder and moved into place from the
  Terminal panel; the host-side guard then reported 7 entries.
- The first commit of this change was refused by its own pre-commit hook: the TEST README quoted a
  fictional company project-folder path. The row now describes the path instead of quoting it.

## Discovered issues

- The hooks only run in clones where `core.hooksPath` is set; older worktrees on branches without
  `.githooks` run none. Every clone and machine needs the regression run once and its own copy of the
  private list.
- Hooks can be bypassed with `--no-verify`; the pre-push hook still checks such commits, but
  `git push --no-verify` skips it too.

## Commands and results

See the [TEST README](../Project_Test/261105_TEST_client-data-guard/README.md): guard clean on the
current tree and blocking on the pre-cleanup tree, hook cases as expected, `PcLive.cs` compiles,
full regression Debug 83/83 and Release 83/83 (319 s), plug-in project builds in Debug and Release
with no warnings.

## Acceptance alignment

All PLAN acceptance items are met; see the TEST table. Live Rhino suites were not run.

## Rollback

`git config --unset core.hooksPath` and revert the commit. The private list can stay.

## Remaining limitations

- No server-side enforcement; the private list is per machine.
- Images already in `Project_Test/` stay (reviewed in the cleanup); only new ones are refused.

## Conclusion

Raw live-run output now goes outside the repository, and commits and pushes carrying client
project data, including real client names that only the private list knows, are refused on this
machine before they reach the public remote.
