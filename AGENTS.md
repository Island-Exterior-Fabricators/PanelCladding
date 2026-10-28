# PanelCladding agent entry

This is the standalone Rhino 8 PanelCladdingEditor repository. Read
`Project_Guides/PanelCladding Architecture.md` and
`Project_Guides/PanelCladding Plan Log.md` before construction work.

- Keep this repository independently buildable; do not add MCP_Rhino dependencies.
- Preserve the product/assembly name `PanelCladdingEditor`, existing command names,
  and plug-in GUID `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35` unless explicitly requested.
- Treat an explicit implementation request as authorization for its required
  PLAN -> EXET -> TEST records. Do not invent new capabilities during runtime use.
- Use a `codex/` branch for construction changes. Keep unrelated user edits intact.
- Keep rerunnable test code in `tests/`; `Project_Test/` folders hold evidence only.
  Update older smokes that a behaviour change affects; the suite has no accepted failures.
- Archive completed work consistently: once a PLAN/EXET/TEST set has completed
  successfully and more than three days have passed since its recorded execution date,
  move the three together, keeping their names, under `Project_Archive/`. Do not
  archive incomplete, failed, blocked, unmatched or still-active work. The plan log has
  the details.
- Verify Debug and Release builds and managed regressions (`tests/Run-Regression.ps1`). Directly
  verify compiled RHP assembly-level GUID metadata before packaging or installation;
  it must be non-empty and match the manifest and plug-in class GUID.
- Production installation requires independent host-persistent registry attestation.
  Without it, build/stage only and leave the prior installed RHP reachable.
  After install/repair/version changes, run the installer with `-Mode Validate`;
  independently check `PlugIn\FileName`, exact `CommandList`, and all three key
  timestamps after the installer exits. After Rhino starts, verify the exact RHP
  loaded and only root/CommandList timestamps advanced. See the registration and
  attestation EXET records for details.
- The Yak package (`Build-PanelCladdingEditorYak.ps1`) is a separate install channel.
  Never publish it to the public McNeel server, and publish to a company source only
  when the user names that source. The zip and Yak installs must not coexist.
- Rhino owns the registration root and CommandList. Installer-owned
  `PlugIn\FileName` does not self-heal. Never infer success from a fresh root alone.
- Do not use Computer Use or Windows UI automation without an explicit request in
  the current user message. Build and managed tests do not authorize UI automation.
- On laptop NYCZBF16G9NXU use portable GitHub CLI from the highest version under
  `%LOCALAPPDATA%\CodexTools\gh\<version>\bin\gh.exe`; do not require admin install.
  Use local git for commits/pushes and the connector for PR creation/merge when available.
