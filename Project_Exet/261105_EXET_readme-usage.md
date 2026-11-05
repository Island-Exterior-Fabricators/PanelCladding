# README features, usage and installation EXET

Record level: lightweight. Executed 2026-11-05, branch `feature/readme-usage` after `b36b7e2`.
Status: complete.

**Reason and scope.** The root README mixed installation with build steps, registry details and
migration provenance, and did not say what the commands do or how to use them. The user asked for
a simple README focused on functionality, usage and installation.

**Changes.** `README.md` only:

- Features: one line per command (all 13), plus the background auto attributes and the
  naming-convention sync.
- How to use: prerequisites (model name, panel geometry, layer standard, workbook), then seven
  steps from `PCtemplatePNL` and `PCpid` through `PCcreate`, `PCeditor`, `PCbake`, sync and
  `PCreport`/`PCweb`, with a link to the Wireframe Web releases page.
- Install: requirements, zip install/upgrade/uninstall (version-neutral file name), Package
  Manager alternative.
- Shorter build and repository sections; the migration provenance paragraph is dropped (its
  inventory and commit map remain under `Project_Archive/`). Detailed behaviour stays in the
  package README, which is what the release zip ships.

**Validation.** Documentation only, no build. Command descriptions checked against the package
README, release notes and command/UI source (selection order, editor buttons, material drop on the
canvas, `PCclear` clearing attributes only). Links resolve to tracked files.
`.githooks/Test-RepositoryContent.ps1 -Mode Staged` and `-Mode Tracked`: clean (849 files);
`tests/Smoke/repository-content-guard`: PASS. This host had no private client list, so the
guard checked file types, folders and path shapes only; the README uses only the fictional `ABC`.
The editor steps were not exercised in Rhino.

**Remaining work and limits.** `core.hooksPath` was unset on this host's clone and is now
`.githooks`. The private client list is not on this host.

**Rollback.** Revert the commit.
