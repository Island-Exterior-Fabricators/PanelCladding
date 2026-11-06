# Panel cladding UD_ user-defined keys EXET

## Corresponding plan and execution date

Executed 2026-10-27 under
[the user-authorized PLAN](../Project_Plan/261027_PLAN_panel-cladding-ud-keys.md).

## Related artifacts

[TEST](../Project_Test/261027_TEST_panel-cladding-ud-keys/README.md) with the managed smoke
`PanelCladdingUdKeysSmoke.csproj`. Branch `codex/panel-cladding-ud-keys` from `master`
(`aad4d54`); no commit or PR was created.

## Implemented scope

- `PanelCladdingKeyService`: `CladdingLogicKey = "UD_CLADDING_LOGIC"`,
  `FrameConfigBackupKey = "UD_FRAME_CONFIG"`; new `LegacyUserDefinedCladdingLogicKey`
  (`CW_9.01_CLADDING_LOGIC`) and `LegacyUserDefinedFrameConfigKey` (`CW_9.02_FRAME_CONFIG`).
  `IsClearableCladdingAssignmentKey` includes the legacy logic key, `IsTopologyKey` the legacy
  frame key, and `TopologyPersistenceMatches` treats a stored legacy frame key as out of date.
- `PanelCladdingKeyService.FrameConfiguration`: `LegacyUserDefinedKey`, `ReadUserDefinedValue`
  (nonblank `UD_` value, else the legacy value), `PlanUserDefinedKeyMigration` (pure delete/write
  plan; a `UD_` value already present wins). `ReadFrameAttribute` falls back to the legacy key, and
  the canonical-read presence check includes it, so decode, canonical read and frame-key
  validation accept legacy panels.
- Application readers: `PCSyncSrf` planning reads stored cladding logic with the fallback; the
  editor save result reads the frame configuration with it (the private `GetUserText` helper in
  `PanelCladdingSaveService` became unused and was removed); `PCMatchSrf` target clean-up treats
  the legacy logic key as transfer configuration.
- Live: `LivePanelCladdingCidService.MigrateUserDefinedKeys` applies the plan and runs first in
  `Normalize`, which the cladding write paths call (`PCEditor` save, `PCCreate`, `PCMatchSrf`/
  `PCMatchCrv`, `PCSyncSrf`, and `PCSpawn*`/`PCUpdate` on their source panels); the
  curve-template write calls it before frame coding. `PCpid` and `PCClear` do not normalize: PCpid
  leaves the keys as they are, PCClear deletes them.
  `LivePanelFrameConfigCodes` seeding and `LivePanelNamingConventionSync` read the configuration
  with the fallback; the curve template "configured" check accepts the legacy key; the
  stale-editor fingerprint includes `UD_` keys and keeps `CW_9.`.
- Comments naming the keys updated; package README key table, cladding-logic and frame-config
  paragraphs updated with the 1.2.2 rename and migration note.
- Version 1.2.2 (`PanelCladdingEditor.csproj`, `package-manifest.json`); command list unchanged.
- Naming-convention smoke: the key-name assertion and messages now use the `UD_` names; its README
  gained a forward note. Its historical results are not rewritten.

## Deviations from plan

None. `CW_9.99_OFFSET_CUSTOM` in the clear and create smokes is a test-only unrelated key; it is
not a plug-in key and was left unchanged (the new smoke also checks it is never migrated).

## Test record

- New smoke, Debug: `PASS: panel cladding UD_ key regression` (4 `[OK]` groups).
- Full managed suite (76 projects including the new one, `dotnet run` per project, stdin empty,
  exit codes recorded), Debug 69/76 and Release 69/76. The 7 failures are the known pre-existing
  ones listed in the 1.2.1 EXET, with the same messages: `PluginAssemblyIdentityProbe` and
  `NativeRuntimeProbe` (need arguments), hide-smoke-command (old command list), type-code-format
  (editor preview shape), pc-curve-display-colors ("must ship as package 1.0.73"), curve-release
  ("Role CID must remain unchanged"), surface-release ("Surface release-only changes must not
  trigger other writes"). The suite rewrote the same 36 screenshots and added the same 2; all were
  restored/removed afterwards (`git status` shows only this change).
- Production project rebuilt after the test-host builds: Debug and Release succeeded, 0 warnings,
  0 errors. `git diff --check`: clean.

## Acceptance alignment

Managed criteria met. The live criterion (legacy panel opens in `PCEditor`, save renames to `UD_`,
Undo restores) needs an installed 1.2.2 inside Rhino and was not run.

## Rollback

Revert the branch. Panels already rewritten by 1.2.2 carry `UD_` keys that 1.2.1 does not read;
rename them back before reinstalling 1.2.1.

## Remaining limitations

- Not packaged, installed or attested; 1.2.1 stays installed. Packaging must re-verify the
  compiled RHP GUID metadata (AGENTS.md).
- A model migrates panel by panel as the plug-in writes it. Panels never written again keep
  `CW_9.01`/`CW_9.02` and keep working. Scripts, schedules or other tools outside this repository
  that read `CW_9.01`/`CW_9.02` must read the `UD_` keys (with a fallback until models are fully
  migrated).
- No whole-model migration command was added (PLAN future extension).

## Conclusion

The plug-in writes `UD_CLADDING_LOGIC` and `UD_FRAME_CONFIG`, still reads panels with
`CW_9.01`/`CW_9.02`, and renames them on the next write. 1.2.2 builds and passes the managed suite
apart from the known pre-existing failures; it is not packaged or installed.
