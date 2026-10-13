# PanelCladding PLAN / EXET / TEST records

Before implementing an explicitly requested construction change, write
`Project_Plan/YYMMDD_PLAN_<capability-name>.md`. Include background, goals,
architecture ownership, design, affected files, usage, acceptance, rollback and
future extensions. Use the user's local date and a lowercase hyphenated name.

Place regression code/evidence in the matching
`Project_Test/YYMMDD_TEST_<capability-name>/` folder. Prefer focused managed smoke
projects and PowerShell verification scripts. Mark tests requiring Rhino/native
initialization or UI separately; a build does not prove live acceptance.

After execution, write `Project_Exet/YYMMDD_EXET_<capability-name>.md` with links to
PLAN and TEST, execution date, changes, deviations, discovered issues, actual
commands/results, acceptance alignment, rollback, remaining limitations and conclusion.
Do not invent test results or silently rewrite historical evidence.

Archive a completed matching trio together, with original names, only after more
than three days since execution. Use `Project_Archive/Project_Plan`,
`Project_Archive/Project_Exet` and `Project_Archive/Project_Test`. Keep incomplete,
failed, current, unmatched or still-active work in place. Repository migration is
not archival and preserves the original active/archive classification.
