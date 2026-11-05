# Record levels and branch prefix EXET

Record level: lightweight. Executed 2026-11-05 on host NOVAN-HOME, branch
`feature/client-data-guard` after `6592050`. Status: complete.

**Reason and scope.** Every explicitly requested change needed a full PLAN -> EXET -> TEST set,
including documentation edits and small fixes, and TEST folders were written even when their
content fit the EXET. MCP_Rhino and Wireframe Web already scale their records to the work. The
user asked to bring that rule here and to use a neutral branch prefix instead of `codex/`.

**Changes.**

- `Project_Guides/PanelCladding Plan Log.md`: sections for the record level, PLAN, tests and
  evidence, EXET and archive. Substantial work (commands, document data, identity, packaging,
  installation, releases, migrations, new tooling) keeps PLAN and EXET, with a TEST folder only for
  evidence that does not fit the EXET; small work gets one EXET marked `Record level: lightweight`;
  runtime use and git housekeeping get none. The level never lowers validation; a
  documentation-only change needs no build. A lightweight EXET archives on its own; older sets are
  not relabelled. Adapted from MCP_Rhino's plan log: here `Project_Test/` holds evidence only and
  test code stays in `tests/`.
- `AGENTS.md`: records by level, branches named `feature/<capability-name>`, archive and validation
  bullets to match.
- `tests/README.md`, `tests/Live/README.md`: evidence goes to the EXET or the TEST folder when there
  is one.
- `Project_Exet/261105_EXET_client-data-guard.md`: `Record level: full` and the new branch name.
- Branch `codex/client-data-guard` renamed to `feature/client-data-guard` (never pushed). Other
  existing `codex/` branches and their records keep their names.

**Validation.** Documentation only, no build. Diff and links reviewed;
`.githooks/Test-RepositoryContent.ps1 -Mode Tracked` clean; no smoke reads the changed text.

**Remaining work and limits.** Records written before 2026-11-05 stay full sets. The other
worktrees' `codex/` branches are unchanged.

**Rollback.** Revert the commit; `git branch -m` restores the old branch name if needed.
