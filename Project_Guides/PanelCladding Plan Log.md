# PanelCladding PLAN / EXET / TEST records

Before implementing an explicitly requested construction change, write
`Project_Plan/YYMMDD_PLAN_<capability-name>.md`. Include background, goals,
architecture ownership, design, affected files, usage, acceptance, rollback and
future extensions. Use the user's local date and a lowercase hyphenated name.

Put rerunnable test code in `tests/` (see `tests/README.md`): a managed smoke in
`tests/Smoke/<capability-name>/`, package checks in `tests/Package/`. Prefer focused
managed smoke projects and PowerShell verification scripts. Mark tests requiring
Rhino/native initialization or UI separately; a build does not prove live acceptance.
Place the change's evidence (commands and results, screenshots, summaries) in the
matching `Project_Test/YYMMDD_TEST_<capability-name>/` folder; it holds no code that
is built or rerun. When a change alters behaviour that older smokes check, update
those smokes in the same change; the full suite has no accepted failures.

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
