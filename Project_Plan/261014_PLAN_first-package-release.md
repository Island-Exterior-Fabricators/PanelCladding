# First downloadable package release

Execution date: 2026-10-14 (America/New_York).

## Background and goals

The standalone repository has no GitHub releases. Publish its first ready-to-use
Windows x64 / Rhino 8 package and verify installation without administrator access.
Continue the existing package sequence as 1.0.97; retain PanelCladdingEditor,
all twelve commands and GUID 7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35.

## Ownership and design

Packaging/PanelCladdingEditor owns the release archive, checksums, launchers,
installation documentation and current-user installer lifecycle. The application
project owns assembly version metadata; the existing PE metadata probe owns compiled
assembly/class identity verification. No runtime capabilities or MCP dependencies
are added. Use LocalAppData and HKCU, with no elevation or machine policy changes.

Build a ZIP containing the complete bundle, quick-start instructions and convenient
install/uninstall launchers. Verify compiled GUIDs before bundling, all payload hashes,
Windows-only dependencies and assembly/package versions. Fix the stale uninstaller
ownership check (it incorrectly requires a shorthand root FileName) and preserve
WhatIf semantics. Publish the tested archive and SHA-256 sidecar on GitHub.

Release-test finding: also preflight registration ownership before moving files and
restore the prior HKCU registration if activation fails. Test refusal of a foreign
registration and inject an ownership-manifest write failure to verify rollback.

## Affected files

- Packaging/PanelCladdingEditor scripts, manifest and documentation.
- src/PanelCladdingEditor/PanelCladdingEditor.csproj version metadata.
- Existing assembly identity probe and verifier.
- README.md and matching PLAN / EXET / TEST records.

## Usage and acceptance

Extract the release ZIP, close Rhino, run Install.cmd as the normal Windows user,
then start Rhino and run PCEditor. Provide repair, validation and uninstall commands.
The default installation requires no SDK, admin rights or global execution-policy change;
Rhino 8 with its .NET 8 runtime must already be available.

Acceptance: Debug and Release builds, relevant managed smoke suites, compiled RHP
assembly/class/manifest GUID agreement, package integrity, and install/repair/validate/
uninstall under a verified non-elevated token using isolated user filesystem and HKCU
roots. Reject HKLM and invalid registrations; preserve modified/unowned files.
Record exact published assets and hashes. Run production RHP rebuilds after DLL-host
tests. Do not change the installed production plug-in or start/automate Rhino; this
release task does not need production activation. Live Rhino acceptance is separate.

## Rollback and future work

Before publication, discard the generated package and revert the focused commit.
After publication, retain versioned immutable assets; issue a corrected version if
necessary. Existing installed RHP and registration remain reachable. Production
installation remains subject to independent host-persistent registry attestation.
Future releases can automate the same verified packaging steps in CI.
