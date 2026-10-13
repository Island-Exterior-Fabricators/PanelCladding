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
- Verify Debug and Release builds and relevant managed regression tests. Directly
  verify compiled RHP assembly-level GUID metadata before packaging or installation;
  it must be non-empty and match the manifest and plug-in class GUID.
- Production installation requires independent host-persistent registry attestation.
  Without it, build/stage only and leave the prior installed RHP reachable.
  After install/repair/version changes, run the installer with `-Mode Validate`;
  independently check `PlugIn\FileName`, exact `CommandList`, and all three key
  timestamps after the installer exits. After Rhino starts, verify the exact RHP
  loaded and only root/CommandList timestamps advanced. See the registration and
  attestation EXET records for details.
- Rhino owns the registration root and CommandList. Installer-owned
  `PlugIn\FileName` does not self-heal. Never infer success from a fresh root alone.
- Do not use Computer Use or Windows UI automation without an explicit request in
  the current user message. Build and managed tests do not authorize UI automation.
- On laptop NYCZBF16G9NXU use portable GitHub CLI from the highest version under
  `%LOCALAPPDATA%\CodexTools\gh\<version>\bin\gh.exe`; do not require admin install.
  Use local git for commits/pushes and the connector for PR creation/merge when available.
