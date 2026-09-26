# Official MCP SDK feasibility: local validation ledger

**Protocol-core proof passed; candidate acceptance failed. This is not merge-ready.**

The final official SDK proof passed all 32 expected cases across 19 methods,
with zero skips. The independent retained HTTP/legacy-dispatch selector passed
89 cases on both baseline and candidate. However, two candidate full Shared
runs failed an existing HTTP disposal assertion, and a reduced interaction
selector reproduced that failure. The exact exception producer and root cause
are **UNKNOWN**. This is an unresolved regression risk, not a proven baseline
flake and not hidden by the separate npm TLS build failure.

## Scope and provenance

Baseline is `273b0182745a3093c0e09f306ca8a1fff6ef3c5a`. It was extracted with
`git archive` inside the isolated worktree, never copied from another proposal.
The final tested index tree was `d2bf4410b0feab640e2a40a21cfd191f54f16c4c`.
Implementation commit:
[`74aa38b74844a973e75cec374c7348c84a483b54`](https://github.com/luisquintanilla/openclaw-windows-node/commit/74aa38b74844a973e75cec374c7348c84a483b54).
Its working-tree production/proof source bytes match the recorded final-run
SHA256s. All six committed input blobs differ only by Git's CRLF-to-LF
normalization; normalized content equality was checked explicitly.
Documentation and evidence packaging after release did not change those files.

Validation ran locally on Windows x64, .NET SDK 10.0.401 / runtime 10.0.12,
after the exclusive host grant. All commands finished and the task-owned
compiler was stopped before host release at **2026-09-26T10:47:00.5555539Z**.
Existing unrelated language servers were left untouched.
**No hosted workflow ran**, and no workflow was pushed by this workstream.
No benchmark, live tray, gateway, or sensitive tool invocation was performed.

The actual net10.0 graph in [packages.json](packages.json) resolves:
Core 2.2.0, AI.Abstractions 10.8.3, Logging.Abstractions 10.0.10, and
DependencyInjection.Abstractions 10.0.10. Normal NuGet restore succeeded.
No v2-feed recovery, alternate feed, TLS bypass, or dependency workaround was
needed. Core's AI 10.8.3 floor does not downgrade the independent speech
proposals' 10.9.0 pins. Production package references remain unchanged.

[Runtime host resolution](sdk-host-resolution.log) places the proof's Core DLL
on the testhost dependency path. [provenance.json](provenance.json) records its
ProductVersion `2.2.0+6fa3825973949a9c4f0cd8af344e15a8db09dc35` and SHA256
`b1752096ea9d2f38ba88e467f04aea43fb300d9a54d881c165f62b69028ee3ab`.
This connects the exercised dependency to the immutable
[SDK implementation](https://github.com/modelcontextprotocol/csharp-sdk/blob/6fa3825973949a9c4f0cd8af344e15a8db09dc35/src/ModelContextProtocol.Core/Server/McpServerImpl.cs),
not just an options comment.

Final candidate Shared DLL SHA256:
`21c5701bbb0e7ea1d0578db40030c61130127def097c50b895ca23611da2f3fd`.
Final SDK test DLL SHA256:
`cb7d1729f84299114a58e3faed9a0355eebb05691d3f7630e14da3af6f018551`.
Baseline Shared DLL SHA256:
`3e26d5a2be4c038b1975e089614e301dbd47b971831d450f90c743f218d80050`.

## Ordered execution record

[chronology.json](chronology.json) contains all 28 retained build, test,
diagnostic, environment, and provenance invocations: complete argument arrays,
original start/end timestamps, durations, exits, recorded source hashes/trees,
failure names, skipped-test names, and raw TRX hashes. Commands use `[checkout]`
and `[session-files]` for redacted roots, not executable literal paths.
Each run's command JSON and output are inside
[sanitized-records.zip](sanitized-records.zip).

Times below are UTC on September 26, 2026. Counts are **passed / failed / skipped**;
non-test commands have no invented test count.

| UTC start | Run name | Exit | Result |
|---|---|---|---|
| 10:22:19 | sdk-initial | 1 | CS9007 raw interpolated JSON delimiter compile failure; no test execution |
| 10:22:58 | sdk-second | 1 | CS0246 misplaced nested LifecycleTransport helper; no test execution |
| 10:23:28 | sdk-third | 1 | 31 / 1 / 0; incorrect fallback expectation for 2099-01-01 |
| 10:24:18 | sdk-final | 0 | 32 / 0 / 0 after correcting the expectation to verified SDK policy |
| 10:24:39 | baseline-mcp | 0 | 89 / 0 / 0 |
| 10:25:16 | candidate-mcp | 0 | 89 / 0 / 0 |
| 10:25:47 | full-build | 1 | Shared, CLI, WinNode CLI, SetupEngine succeeded; WinUI post-build npm restore failed |
| 10:29:18 | shared-full | 1 | 4,106 / 1 / 32; MXC TEMP ACL-sensitive grant assertion |
| 10:31:38 | temp-acl-recovery | 0 | Bounded current-user ACE on owned task TEMP only |
| 10:31:48 | shared-final | 1 | 4,106 / 1 / 32; unresolved HTTP disposal assertion |
| 10:33:15 | baseline-shared-full | 0 | 4,107 / 0 / 32 |
| 10:34:44 | tray-initial | 1 | 3,071 / 1 / 0; app-local CRT absent after interrupted build targets |
| 10:36:38 | tray-targetdir | 0 | Actual default-platform win-x64 TargetDir recorded |
| 10:36:47 | tray-runtime-recovery | 0 | Existing runtime/loose-asset copy targets only |
| 10:36:56 | tray-final | 0 | 3,072 / 0 / 0 |
| 10:38:22 | shared-confirmed | 1 | 4,106 / 1 / 32; same unresolved HTTP disposal assertion |
| 10:39:43 | winnode-initial | 0 | 127 / 0 / 0; actual first-run restore/build and execution |
| 10:40:45 | baseline-http-drain-interaction | 0 | 32 / 0 / 0 |
| 10:42:00 | baseline-drain-diagnostic | 0 | Inherited-pipe reflection probe; zero unobserved events |
| 10:42:23 | candidate-drain-diagnostic | 0 | Same probe; zero unobserved events |
| 10:42:34 | candidate-http-drain-interaction | 1 | 31 / 1 / 0; same unresolved HTTP disposal assertion |
| 10:43:30 | baseline-pre-canceled-diagnostic | 0 | Pre-canceled reflection probe; zero unobserved events |
| 10:43:42 | candidate-pre-canceled-diagnostic | 0 | Same probe; zero unobserved events |
| 10:44:32 | sdk-confirmed | 0 | 32 / 0 / 0; final source and host-resolution confirmation |
| 10:45:00 | candidate-mcp-confirmed | 0 | 89 / 0 / 0; independent retained HTTP confirmation |
| 10:45:27 | sdk-packages | 0 | Restored package graph |
| 10:45:27 | dotnet-environment | 0 | Installed SDK/runtime identity |
| 10:46:17 | collect-provenance | 0 | Exact 19-method/32-case inventory and source/binary/TRX hashes |

The MCP selector is exactly:

```text
FullyQualifiedName~McpToolBridgeTests|FullyQualifiedName~McpHttpServerTests|FullyQualifiedName~McpHttpServerTelemetryTests
```

The reduced interaction selector is exactly:

```text
FullyQualifiedName~BoundedProcessWaitTests|FullyQualifiedName~McpHttpServerTests
```

Both use `dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj`.
Full required commands were:

```powershell
.\build.ps1 -NoTrustRepository
dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj --no-restore
dotnet test .\tests\OpenClaw.Tray.Tests\OpenClaw.Tray.Tests.csproj --no-restore
dotnet test .\tests\OpenClaw.WinNode.Cli.Tests\OpenClaw.WinNode.Cli.Tests.csproj
dotnet test .\tests\OpenClaw.Mcp.Sdk.Tests\OpenClaw.Mcp.Sdk.Tests.csproj --no-restore
```

First-run test projects restored/built rather than silently no-oping with absent
outputs. Every actual test run has a nonempty TRX. The existing full Shared
suites contain 32 NotExecuted result entries even though the VSTest summary
counter `notExecuted` is zero; published skip counts use the actual result entries.

## Failures, recovery, and diagnosis

The unresolved failing test is
`McpHttpServerTests.Dispose_DuringInFlightHandler_DoesNotSurfaceObjectDisposedException`,
at the `Assert.DoesNotContain(unobserved, e => e is ObjectDisposedException)`
assertion. Captured stacks are `FileStream.ReadAsync`,
`StreamReader.ReadBufferAsync`, and `StreamReader.ReadToEndAsyncInternal`.
That test subscribes to process-global `TaskScheduler.UnobservedTaskException`;
the stack alone does not identify the producer.

The HTTP host and its tests are unchanged. The extracted bridge path retains
the existing fake capability execution and has no process/FileStream cleanup.
`BoundedProcessWait.cs` is byte-identical on baseline and candidate. Nevertheless,
these facts do **not** establish that the extraction is independent of the
failure. The reduced filter fails on candidate and passes on baseline.
Standalone probes of inherited-pipe and pre-canceled process waits produced
zero events on both, so the suspected producer was not established. Their final
diagnostic source and output are retained. No production fix was made on that
unproven hypothesis. No test was excluded, parallelism was not disabled, and no
exception handler was added to suppress a failing test. The standalone probes
used GC to expose pending events, not to hide them before an assertion.

The distinct initial Shared failure was
`MxcConfigBuilderTests.Build_BootstrapsShellPathAndGrantsBackendSafePathDirsReadonly`.
Recovery verified the exact task TEMP, current-user ownership, and no reparse
ancestor, preserved prior ACL evidence locally, then added only a current-SID
inheritable Allow FullControl ACE while retaining every existing ACE/inheritance
setting. No parent/shared directory, owner, global ACL, or production policy changed.
The initial failure remains in its own TRX.

The initial Tray failure was
`NativeSpeechStackRuntimeTests.TrayBuildOutput_NativeTtsStack_LoadsWithAppLocalVCRuntime`.
The full build had compiled the actual Tray DLL before npm stopped post-build
targets. After recording the evaluated `bin\Debug\...\win-x64` TargetDir, recovery
ran only:

```powershell
dotnet msbuild .\src\OpenClaw.Tray.WinUI\OpenClaw.Tray.WinUI.csproj `
  "-t:CopyOpenClawVCRuntimeToOutput;CopySetupAssetsLooseForConsumerCopy" `
  -p:RuntimeIdentifier=win-x64 -p:SkipMxcNodeBridgeRestore=true -verbosity:minimal
```

No `Platform=x64`, arbitrary DLL copying, or `BuildProjectReferences=false` was
used. Existing VS discovery used a process-only installed VS override.
These copy targets are test-environment recovery, **not** full-build, release,
native inference, or MXC execution proof.

The single full build failed downloading locked `node-pty-1.2.0-beta.12.tgz`
from the official npm registry with `ERR_SSL_SSL/TLS_ALERT_HANDSHAKE_FAILURE`.
That endpoint was not retried, and no lockfile/TLS/feed/security setting changed.
This failure is separate from the unresolved candidate test failure.

## Exact protocol evidence

Four real official client/server cases negotiate `2024-11-05`, `2025-03-26`,
`2025-06-18`, and `2025-11-25`. The independently authored unsupported-version
request remains unchanged:

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2099-01-01","capabilities":{},"clientInfo":{"name":"raw-proof","version":"1"}}}
```

Actual pinned SDK response, preserved in the TRX output:

```json
{"error":{"code":-32022,"message":"Protocol version \u00272099-01-01\u0027 is not available through the initialize handshake.","data":{"supported":["2024-11-05","2025-03-26","2025-06-18","2025-11-25"],"requested":"2099-01-01"}},"id":1,"jsonrpc":"2.0"}
```

`ValidateInitializeRequestBoundary` throws
`UnsupportedProtocolVersionException` for this unsupported handshake.
The initial expected fallback was incorrect. The corrected test asserts the
error code, requested version, exact four-version list, and absent result.
It does not remove or weaken the 2099 request. Historical proof correction
patches, compile failures, and the failed 31/32 run are retained in the archive.

[sdk-inventory.json](sdk-inventory.json) lists every expected method/count and
all 32 passing case names. The three delivery/lifecycle cases are:
`CancellationAfterHandlerReturn_CompletesDeliveryBeforeShutdown` (real SDK filter
and deterministic gates), `DisposeFailure_CompletesPendingDeliveryExactlyOnce`,
and `LateResponse_DoesNotClaimReusedIdDelivery` (direct fault injection).
They do not prove broad transport race freedom, global duplicate-ID admission,
or cross-client cancellation/tombstone parity.

## Evidence integrity and limitations

[file-hashes.json](file-hashes.json) maps all 76 original records to raw and
sanitized SHA256s; [archive-hash.json](archive-hash.json) fingerprints the ZIP.
Raw TRX/logs remain local and were not published. The archive contains sanitized
TRX, command JSON, logs, proof inventory, binary metadata, and bounded diagnostic
source. XML/JSON were reparsed before export, and ZIP-contained TRX reparsed
again. No partial failed-redaction output was published.

Replacements remove host profile/worktree/session paths, machine/run-user names,
installed-tool roots, bearer values, and fake secret markers/token-shaped strings
from existing redaction-test case names. Known synthetic test profiles
alice/bob/carol/dev/name remain as fixture input, not host data.
ACL SDDL/SIDs, user settings, full runtime host trace, and prepared workflow
artifacts remain unpublished. The three-line host resolution excerpt is retained.

The command runner captured source hashes before each invocation and git
metadata afterwards. No source edit occurred during a run. The baseline archive
has no independent `.git`; its command JSON's `indexTree` describes the outer
candidate index and must **not** be used as baseline identity. Use the explicit
baseline commit/archive hash and baseline source hashes instead. Diagnostic
commands name the actual baseline/candidate DLL arguments; their runner's source
hash field describes the current checkout, not the reflected assembly.

Binary hashes were collected after final runs, not before/after every command.
The retained final DLLs and their original write times are identified, but
overwritten intermediate proof DLLs were not preserved and must not be inferred.
The two compile failures executed no tests. The first diagnostic source revision
was not separately snapshotted; the final source includes the additional
pre-canceled mode. Diagnostic zero-event outcomes are negative evidence only.
Internal child-build shell boundaries, the initial read-only probes, and the
ACL preflight have no independent original start/end ledger; only the captured
top-level command boundaries and available runner timestamps are asserted.
No workload was rerun merely to manufacture missing provenance.

Read-only parent/rubber-duck source review informed the ownership and cancellation
fixes. Codex autoreview was not run (the CLI was unavailable and installation
was prohibited); no clean Codex review is claimed. Read-only upstream issue/PR
searches for `ModelContextProtocol` and `"MCP SDK"` returned no matches on
September 26, 2026; that is not proof no differently worded work exists.

The evidence does not validate migrated HTTP, full 2026 initialize-less
lifecycle, real OS consent/exec approvals/MXC, gateway, UI, ARM64, performance,
or security certification. Full adoption remains a maintainer design decision,
and the failed candidate acceptance remains unresolved.
