# First downloadable package release execution

- Execution date: 2026-10-14 (America/New_York).
- [PLAN](../Project_Plan/261014_PLAN_first-package-release.md)
- [TEST and reproducible commands](../Project_Test/261014_TEST_first-package-release/README.md)
- Release target: [PanelCladdingEditor v1.0.97](https://github.com/novan-xu/PanelCladding/releases/tag/v1.0.97).

## Changes

Prepared version 1.0.97 as the standalone repository's first GitHub release,
continuing the previous internal 1.0.96 version sequence. Assembly metadata now
matches the manifest. Product/assembly name, plug-in GUID and twelve commands
remain unchanged; no application capability or MCP dependency was added.

The builder validates the compiled assembly and class GUIDs and version before
bundling, resolves output paths, and creates a Windows x64 ZIP with a SHA-256 sidecar.
The bundle contains all private dependencies, a quick start, workflow documentation,
and normal-user Install.cmd / Uninstall.cmd launchers. Install.cmd checks that Rhino
is closed and runs install followed by Validate. Installation needs no SDK,
elevation or machine-wide PowerShell policy change. Rhino 8 and .NET 8 are prerequisites.

Fixed uninstaller ownership for the canonical child-only FileName registration,
WhatIf cleanup/manifest behavior, and the Windows PowerShell 5.1 hash-read issue.
Release tests also exposed a refused-repair path that could delete foreign
registration: ownership is now checked before file moves, and a prior HKCU export
is restored when activation fails. This installer correction is the only scope
extension beyond the initial plan, and the plan records it explicitly.

## Commands and outcomes

The focused Verify-Release.ps1 runner and its constituent commands passed six
managed suites in Debug and Release, both solution builds, final direct RHP
rebuilds, compiled PE identity/version checks, archive checks, non-elevated Windows
PowerShell 5.1 installation lifecycle tests, and the existing registration smoke.
See TEST for exact suite names, commands, environment and corrected initial failures.
Builds reported zero warnings and errors. All 32 payload hashes matched.

The lifecycle test read registry values and all three timestamps after installer
process exit and independently queried the child RHP pointer with reg.exe. It also
injected a post-activation failure and confirmed successful rollback/validation.
These are isolated fixture results, not production host activation attestation.

## Acceptance, rollback and limits

The release package and per-user installer satisfy the managed and installation
acceptance gates. GitHub's versioned release and checksum asset track the published
archive; release publication follows the final committed-source package check.

No production RHP or registration was changed, and no Rhino session or UI automation
was started. Existing installed versions remain reachable. Live native/UI acceptance
is separate and was not rerun. Production activation still requires the independent
host-persistent attestation and post-Rhino-start checks described in AGENTS.md.

Before publication, rollback consists of reverting the focused change and discarding
generated artifacts. After publication, retain the immutable versioned assets and
issue a corrected version when needed. Installation rollback retains prior files
and registration evidence; uninstall retains modified files and migration backups.
