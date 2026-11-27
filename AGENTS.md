# PanelCladding agent entry

This repository holds the standalone Rhino 8 plug-in `PanelCladdingEditor`.
This page is advice; the rules that must hold are checks in the repository, listed in the [design document](docs/design.md#enforced-rules).

## Check your work

- Run `powershell -NoProfile -ExecutionPolicy Bypass -File tools/check.ps1` before you open a pull request; CI, Super Agent and the pre-push hook run the same command.
- Follow the change and pull request flow in [CONTRIBUTING.md](CONTRIBUTING.md).

## Read before you change

- [docs/design.md](docs/design.md) for the model, its invariants and the enforced rules.
- [docs/decisions.md](docs/decisions.md) before you reverse an existing behaviour or convention; add a new entry at the end when you make a deliberate choice.
- [docs/operations.md](docs/operations.md) before build, packaging, installation or release work.

## Suggestions

- Keep the product name `PanelCladdingEditor`, the command names and the plug-in GUID `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35` unless the owner asks otherwise.
- Keep the repository independently buildable, without MCP_Rhino dependencies.
- Prefer fictional project codes (`ABC`, `XYZ`) everywhere, since the repository is public; keep raw live-run output under `%LOCALAPPDATA%\PanelCladding\LiveEvidence`, and never bypass the hooks with `--no-verify`.
- Treat a production installation as unfinished until the host attestation in the [operations guide](docs/operations.md#production-install-and-host-attestation) has passed; without it, build and stage only.
- Publish the Yak package only to a company source the owner names, never to the public McNeel server, and do not let the zip and Yak installs coexist.
- During runtime use of the plug-in, work with the capabilities it has rather than inventing new ones.
- Use Computer Use or Windows UI automation only when the current request asks for it.
- On laptop NYCZBF16G9NXU, the portable GitHub CLI is the highest version under `%LOCALAPPDATA%\CodexTools\gh\<version>\bin\gh.exe`.
