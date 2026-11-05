# PanelCladding PLAN / EXET / TEST records

Before implementing an explicitly requested construction change, write
`Project_Plan/YYMMDD_PLAN_<capability-name>.md`. Include background, goals,
architecture ownership, design, affected files, usage, acceptance, rollback and
future extensions. Use the user's local date and a lowercase hyphenated name.

Put rerunnable test code in `tests/` (see `tests/README.md`): a managed smoke in
`tests/Smoke/<capability-name>/`, package checks in `tests/Package/`. Prefer focused
managed smoke projects and PowerShell verification scripts. Mark tests requiring
Rhino/native initialization or UI separately; a build does not prove live acceptance.
Place the change's evidence (commands and results, summaries) in the matching
`Project_Test/YYMMDD_TEST_<capability-name>/` folder; it holds no code that is built or
rerun, and since 2026-11-05 no new images: describe what a capture showed instead. When a change alters behaviour that older smokes check, update
those smokes in the same change; the full suite has no accepted failures.

The repository is public, so records, fixtures and evidence carry no client project data.
Use fictional project codes (`ABC`, `XYZ`; `QRS`, `JKL` for another project) and generic
paths such as `C:\Projects\ABC\ABC - Wireframe.3dm`, never real project names, numbers or
company file-server paths. A live run against a real project model commits only sanitized
conclusions (step names, pass/fail, counts); its raw output (exported workbooks,
screenshots, object and layer dumps) stays out of the repository and is deleted after
the run. The live suites write it to `%LOCALAPPDATA%\PanelCladding\LiveEvidence` and refuse
a folder inside a git working tree. The hooks in `.githooks` (pre-commit, commit-msg,
pre-push; `tests/Run-Regression.ps1` sets `core.hooksPath`) run
`.githooks/Test-RepositoryContent.ps1` with the rules in `.githooks/content-rules.json`
(project data file types, `results*`/`live*` folders and new images under `Project_Test/`,
company project-folder paths) and a private client list outside the repository
(`%APPDATA%\PanelCladding\content-denylist.txt`, or `PANELCLADDING_CONTENT_DENYLIST`) that
names each real client project's name, code and number. Add every new client project to that
list; never commit it or bypass the hooks. `tests/Smoke/repository-content-guard` checks the
rules, the hook wiring, the tracked files and the hooks' behaviour.

After execution, write `Project_Exet/YYMMDD_EXET_<capability-name>.md` with links to
PLAN and TEST, execution date, changes, deviations, discovered issues, actual
commands/results, acceptance alignment, rollback, remaining limitations and conclusion.
Do not invent test results or silently rewrite historical evidence.

Archive completed work consistently. Keep active records in `Project_Plan/`,
`Project_Exet/` and `Project_Test/`. Once work has completed successfully and more
than three days have passed since its recorded execution date (including dated
addenda), move the matching PLAN, EXET and TEST together into
`Project_Archive/Project_Plan`, `Project_Archive/Project_Exet` and
`Project_Archive/Project_Test`, keeping their names. Do not archive incomplete,
failed, blocked, unmatched or still-active work; required acceptance that the EXET
records as pending, including live Rhino checks, keeps work active. Fix relative
links the move breaks, and record each archive pass in
`Project_Archive/YYMMDD_archive-manifest.md` with the sets moved and the older sets
kept, with reasons. Repository migration is not archival and preserves the original
active/archive classification.
