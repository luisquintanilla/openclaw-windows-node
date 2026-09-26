# Five audio schemas: local evidence

This is bounded discussion evidence for a discovery-only pilot, not full
product or merge validation. No microphone, speech inference, audible playback,
model download or cloud AI call was used for the schema proof.

## Source provenance

| Role | Immutable commit/blob |
|---|---|
| Inspected upstream base | `273b0182745a3093c0e09f306ca8a1fff6ef3c5a` |
| B: base plus characterization harness only | `01956ec2f31fcc1d5145ee387be35ac4476d7fd6` |
| C: discovery helper, one-line hookup and documentation | `a993bc9e08fe1815f6080e571570f82d19f00bc3` |
| Identical test-file blob in B and C | `8ec3c9379dc2f132b258f3908d81249427ffd634` |

B's production `src` tree equals the base, `e105f5679ef7248e2835011e6240d82b4214f324`.
C's production tree is `545e6e673c557206335fdae0507731b3d809b256`.
Only the prior-art documentation correction was dirty during C's runs.
The later evidence/documentation commit does not alter source or tests.
The workflow-only validation branch was not published: GitHub rejected its
push for missing `workflow` scope. No hosted run URL exists. A separate cloud
pilot never confirmed execution. Neither is evidence.

## Actual results

All test counts below come from nonempty TRX files. The checked-in
[command records](command-results.json) include exact argument arrays, source
SHA, UTC start/end, exit codes, raw-TRX hashes and named nonpassing outcomes.
The [method inventory](test-inventory.csv) includes theory-case counts per
method and run, without publishing theory inputs or unrelated test stdout.
VSTest's summary `notExecuted` was zero even for xUnit skips; skipped totals
are counted from individual `NotExecuted` results, not inferred from exit 0.

| Run | Exit | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| B characterization | 0 | 75 | 75 | 0 | 0 |
| C focused schema/bridge/STT/TTS | 0 | 202 | 202 | 0 | 0 |
| Shared full, initial | 0 | 4220 | 4188 | 0 | 32 |
| Tray full, initial | 1 | 3072 | 3071 | 1 | 0 |
| Tray runtime guards, wrong-output-path recovery | 1 | 4 | 3 | 1 | 0 |
| Tray full, final recovery | 0 | 3072 | 3072 | 0 | 0 |
| WinNode CLI full | 0 | 127 | 127 | 0 | 0 |
| Shared full, final | 0 | 4220 | 4188 | 0 | 32 |

The focused inventory is 81 `McpAudioToolSchemaTests`, 47
`McpToolBridgeTests`, 42 `SttCapabilityTests`, and 32 `TtsCapabilityTests`.
B excludes the six new-schema assertions by method prefix
`ToolsList_AudioSchema`; it includes all 75 characterization/export/discovery
cases. C includes those same cases plus all six schema assertions.
This is not a baseline full-suite run.

The 32 Shared skips are existing opt-in integration tests. Their names and
original gate messages are retained in `command-results.json`. No test,
expectation, filter or skip attribute was loosened to obtain the final result.

## Before and after

[Before tools/list](audio-tools-before.json) and
[after tools/list](audio-tools-after.json) are actual responses from
`McpToolBridge.HandleRequestAsync` with real registered STT/TTS capabilities.
The five `inputSchema` values change; every other audio catalog field is
identical. Dynamic removal and the generic fallback for unrelated tools are
also exercised by the harness.

[Before calls](audio-calls-before.txt) and
[after calls](audio-calls-after.txt) contain seven identical JSON-RPC request
and response pairs: five successes and two validation errors. The bridge and
capability parsers are real; audio handlers return fixed synthetic results.
The TTS result deliberately tests propagation of a fake result, not whether
a particular real provider would succeed or fall back. Provider strictness
is checked separately against the existing resolution policy.

[Validator outcomes](schema-validation.json) record 21/21 matched
expectations across all five schemas using the already-installed PowerShell
7.6.6 `Test-Json` and JsonSchema.Net 7.0.0.0. Draft 2020-12 was added to the
validator's copy only; the emitted schemas do not gain a `$schema` field.

Some intentionally accepted schema inputs still fail at runtime: decimal or
exponent integer tokens for transcribe, whitespace-only TTS text, and text
exceeding the trimmed UTF-16 limit. Conversely, listen's out-of-range Int32
values clamp and optional wrong-type values select defaults. The metadata
documents these parser boundaries rather than introducing incompatible raw
constraints. See [the contract table](../../MCP_AUDIO_SCHEMAS.md#compatibility-boundaries).

## Reproduce

Use separate isolated checkouts of B and C. Set `OPENCLAW_REPO_ROOT` to each
checkout and `OPENCLAW_TRAY_DATA_DIR` to a disposable task-owned settings
directory. The recorded host used Windows x64, .NET SDK 10.0.401, SDK-style
xUnit v2/VSTest, a short process-only PATH and task-owned TEMP/TMP. Dependency
restores used a session-only official NuGet v2 configuration, not global
source changes, audit disablement or TLS bypass.

The exact recorded commands, with only local paths replaced by labeled
placeholders, are in `command-results.json`. Their essential invocations:

```powershell
# B: no --no-restore on the first run.
dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj --filter "FullyQualifiedName~McpAudioToolSchemaTests&FullyQualifiedName!~ToolsList_AudioSchema" --logger "trx;LogFileName=audio-baseline.trx" --results-directory $results --verbosity minimal

# C: includes all six new schema assertions and existing focused suites.
dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj --filter "FullyQualifiedName~McpAudioToolSchemaTests|FullyQualifiedName~McpToolBridgeTests|FullyQualifiedName~TtsCapabilityTests|FullyQualifiedName~SttCapabilityTests" --logger "trx;LogFileName=audio-candidate.trx" --results-directory $results --verbosity minimal

# Required build attempt. -NoTrustRepository avoids mutating Git trust settings.
.\build.ps1 -NoTrustRepository
dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj --no-restore

# First build avoids the fresh-worktree --no-restore no-op.
dotnet build .\tests\OpenClaw.Tray.Tests\OpenClaw.Tray.Tests.csproj --verbosity minimal
dotnet test .\tests\OpenClaw.Tray.Tests\OpenClaw.Tray.Tests.csproj --no-restore
dotnet build .\tests\OpenClaw.WinNode.Cli.Tests\OpenClaw.WinNode.Cli.Tests.csproj --verbosity minimal
dotnet test .\tests\OpenClaw.WinNode.Cli.Tests\OpenClaw.WinNode.Cli.Tests.csproj --no-restore
```

`ToolsList_ExportsActualAudioCatalog` and
`ToolsCall_ExportsRepresentativeAudioResponses` put the catalog and seven pairs
in their TRX `Output/StdOut`. Compare the two call outputs exactly. In the
catalogs, substitute only the five schemas before comparing the rest.
For each validator case in `schema-validation.json`, use that tool's emitted
schema with the declared `$schema` dialect and evaluate its `arguments` using
`Test-Json`. Compare against `expectedSchemaAcceptance`, not a claim that every
schema-accepted input is a successful audio call.

## Failure and recovery history

The full build exited 1. Shared, CLI, WinNode CLI and SetupEngine built, but
WinUI's existing locked `node-pty-1.2.0-beta.12.tgz` download failed with
`ERR_SSL_SSL/TLS_ALERT_HANDSHAKE_FAILURE`. See [the build log](full-build.log).
There was no further TLS endpoint probing or retry. The full build and its
later packaging/product paths remain unverified.

This left an incomplete WinUI output. Tray's
`NativeSpeechStackRuntimeTests.TrayBuildOutput_NativeTtsStack_LoadsWithAppLocalVCRuntime`
failed because `msvcp140.dll` was absent. The first existing runtime-copy
target invocation used `-p:Platform=x64` and exited 0, but copied to
`bin\x64\Debug`, not the `bin\Debug` output inspected by the test.
The four runtime guards still produced three passes and one failure.

The corrected invocation matched the original build's default platform:

```powershell
dotnet msbuild .\src\OpenClaw.Tray.WinUI\OpenClaw.Tray.WinUI.csproj -t:CopyOpenClawVCRuntimeToOutput -p:RuntimeIdentifier=win-x64 -p:Configuration=Debug -verbosity:minimal -getProperty:TargetDir,OpenClawVCRuntimeArch
```

It used the existing Visual Studio redistributable via the process-only
`OpenClawVsInstallRoot` override. The reported directory matched the failing
guard's `bin\Debug\net10.0-windows10.0.22621.0\win-x64` output. The full
Tray rerun passed all 3072 tests, including all four runtime guards; Shared
was rerun too. No product changes, arbitrary DLL copying, global runtime
installation or test weakening was used. DLL load proof is not speech
inference proof and this recovery does not make the full build successful.

## Coverage boundaries and review

| Requirement | Evidence |
|---|---|
| Correct tool names, fields, required fields and defaults | Six `ToolsList_AudioSchema*` cases plus exact catalogs |
| Disabled capabilities absent; unrelated schema preserved | `ToolsList_LiveRegistry_RemovesDisabledAudioAndPreservesOtherSchemas` |
| Duration bounds, token kinds, language fallback, errors | `ToolsCall_Transcribe_*`, `ToolsCall_Listen_*` |
| Trimmed UTF-16 limit and optional coercion | `ToolsCall_Speak_ValidatesTrimmedUtf16Length`, `ToolsCall_Speak_PreservesOptionalCoercionAndResult` |
| Explicit provider stays strict | `ToolsCall_Speak_PreservesExplicitProviderForStrictResolution` |
| Unknown keys and existing result casing | Characterization cases, status cases and seven exact pairs |
| Public schema interpretation, including limitations | 21 recorded `Test-Json` outcomes |

Direct source review found no actionable issue within the metadata/parser
boundary. The structured helper was attempted once:
`python .\.agents\skills\autoreview\scripts\autoreview --mode branch --base 273b0182745a3093c0e09f306ca8a1fff6ef3c5a --no-web-search --output [session-artifacts]\local-results\autoreview.txt --json-output [session-artifacts]\local-results\autoreview.json`.
It exited 1 because `codex` was not installed.
No alternate engine, account or installation was attempted. This is not a
clean structured-review result.

Not verified: running WinUI/HTTP MCP, gateway-mediated invocation, real audio
engines, cloud providers, client compatibility across the MCP ecosystem, or
agent-success/performance improvements. Full-repo CI contains further suites
outside this local subset. The five schemas are a reviewable pilot, not a
runtime validator or an all-command catalog.

Only allowlisted, path-redacted logs, synthetic audio output, method/count
inventories and metadata were published. Line endings are normalized to LF;
JSON values and request/response contents are unchanged. Raw TRX remains local;
its hashes are recorded. [manifest.json](manifest.json) hashes the generated evidence
files, excluding this narrative README and the manifest itself.
