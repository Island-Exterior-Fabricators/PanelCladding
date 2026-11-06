# PanelCladding agent entry

This is the standalone Rhino 8 PanelCladdingEditor repository. Before construction work read
`Project_Guides/PanelCladding Architecture.md` and `Project_Guides/PanelCladding Workflow.md`;
for build, packaging, installation or release work also read
`Project_Guides/PanelCladding Operations.md`. Check `Project_Guides/PanelCladding Decisions.md`
before reversing an existing behaviour or convention.

- Keep this repository independently buildable; do not add MCP_Rhino dependencies.
- Preserve the product/assembly name `PanelCladdingEditor`, existing command names,
  and plug-in GUID `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35` unless explicitly requested.
- Do not invent new capabilities during runtime use.
- Use a `feature/<capability-name>` branch for construction changes. Keep unrelated user
  edits intact.
- Keep the documents current instead of writing change records: a change updates the guides,
  package README and release notes it affects in the same pull request, and its pull request
  description records what changed, how it was validated and what is left (workflow guide).
  Do not write PLAN/EXET/TEST records or recreate `Project_Plan/`, `Project_Exet/`,
  `Project_Test/` or `Project_Archive/`; the former records are at tag `records-2026-11-06`.
- The repository is public. Never commit client project data: no real project names,
  codes, numbers or file-server paths, and no raw output of live runs on project models
  (exports, screenshots, object dumps). Use fictional codes (`ABC`, `XYZ`) and report
  only sanitized conclusions. Live suites write raw output under
  `%LOCALAPPDATA%\PanelCladding\LiveEvidence`, never into the repository. The hooks in
  `.githooks` (enabled by `tests/Run-Regression.ps1`) refuse such commits and pushes, also
  against a private client list kept outside the repository
  (`%APPDATA%\PanelCladding\content-denylist.txt`; add each new client project there).
  Never bypass them (`--no-verify`); the `repository-content-guard` smoke checks them.
- Keep rerunnable test code in `tests/`. Update older smokes that a behaviour change
  affects; the suite has no accepted failures.
- Verify Debug and Release builds and managed regressions (`tests/Run-Regression.ps1`); a
  documentation-only change needs none. Directly verify compiled RHP assembly-level GUID
  metadata before packaging or installation; it must be non-empty and match the manifest
  and plug-in class GUID.
- Production installation requires independent host-persistent registry attestation.
  Without it, build/stage only and leave the prior installed RHP reachable.
  After install/repair/version changes, run the installer with `-Mode Validate`;
  independently check `PlugIn\FileName`, exact `CommandList`, and all three key
  timestamps after the installer exits. After Rhino starts, verify the exact RHP
  loaded and only root/CommandList timestamps advanced. The operations guide has the
  details.
- The Yak package (`Build-PanelCladdingEditorYak.ps1`) is a separate install channel.
  Never publish it to the public McNeel server, and publish to a company source only
  when the user names that source. The zip and Yak installs must not coexist.
- Rhino rewrites the registration root and CommandList on every start, even when nothing
  loads. The installer-owned `PlugIn\FileName` is never rewritten by Rhino and does not
  self-heal. Never infer success from a fresh root alone.
- Do not use Computer Use or Windows UI automation without an explicit request in
  the current user message. Build and managed tests do not authorize UI automation.
- On laptop NYCZBF16G9NXU use portable GitHub CLI from the highest version under
  `%LOCALAPPDATA%\CodexTools\gh\<version>\bin\gh.exe`; do not require admin install.
  Use local git for commits/pushes and the connector for PR creation/merge when available.
