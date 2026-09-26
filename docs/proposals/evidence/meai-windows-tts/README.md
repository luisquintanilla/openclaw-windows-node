# Windows MEAI TTS pilot evidence

This is local, scoped evidence for the [Windows-only proposal](../../MEAI_WINDOWS_TTS.md),
not CI, complete product playback proof or a merge-ready build.

## Source and environment

- Baseline: `273b0182745a3093c0e09f306ca8a1fff6ef3c5a`.
- Final tested source: `2c4ec89c2196cccce484b97e29cf54d6c5804d7a`.
- Earlier candidates: `f38a504b9a9ce41082e94235d192094daa9a76f9`
  (initial unvalidated pilot), `aee7d39a93050b137f464d45c48bc4f2da12c961`
  (direct blank-text forwarding), `c6a6f4b8fe187abfa7408989a564239face35e5c`
  (nullable test assertion fix and native format evidence).
- The production x64 assembly compiled at `aee7d39a` during command 04.
  Both later candidates changed tests/documentation only:
  `git diff aee7d39a93050b137f464d45c48bc4f2da12c961 2c4ec89c2196cccce484b97e29cf54d6c5804d7a -- src`
  is empty. Command 12 rebuilt the final service-test assembly. Binary hashes
  and the actual package graph are in [validation.json](validation.json).
  Read-only hash comparison also confirmed that the WinUI and MEAI assemblies
  copied into the service-test output are byte-identical to those recorded
  production dependencies.
- Windows 10.0.26200, x64; .NET SDK 10.0.401, runtime 10.0.12;
  xUnit 2.9.3, VSTest, Debug. The service harness uses `win-x64`, `Platform=x64`.
- Isolated worktree, task-owned settings and TEMP, process-only shortened PATH,
  existing Visual Studio 2022 Enterprise selection. Official NuGet v2 restore
  was used after the initial missing-assets failure, not a new package mirror.
  No global settings, certificate, TLS, audit, dependency-pin or credential changes.

Validation ran on 2026-09-26. The command ledger preserves its original UTC
offsets. The last test process exited at `2026-09-26T09:42:41.5984608+00:00`
(`05:42:41.5984608-04:00` local). An initial coordination message mistakenly
attached `Z` to the local clock time; that message is not the timestamp source.
No validation was rerun to repair that wording.

## Results

| Recorded command | Passed | Failed | Skipped | Scope |
|---|---:|---:|---:|---|
| 03 | 46 | 0 | 0 | Adapter plus unchanged Piper contract, ElevenLabs and MiniMax fake-client tests |
| 13 | 26 | 0 | 0 | Final actual-service suite: 23 deterministic cases and exactly 3 native cases |
| 22 | 4,107 | 0 | 32 | Final full Shared suite, including 32 TtsCapability cases |
| 23 | 3,100 | 0 | 0 | Final full Tray suite, including adapter tests |

Do not add overlapping focused and full-suite counts. The 32 Shared skips
are existing opt-in groups: 16 device-identity, 9 local-command integration
and 7 MXC integration cases. They are not native TTS skips or passing proof.

The one mandatory full build (command 11) **failed**. Its Shared, CLI,
WinNode CLI, SetupEngine and documentation stages succeeded; WinUI failed on
`ERR_SSL_SSL/TLS_ALERT_HANDSHAKE_FAILURE` fetching the existing locked
`node-pty-1.2.0-beta.12.tgz` from the npm registry. This was not retried,
bypassed or repaired by changing dependencies.

## Exact silent native inventory

Command 13 explicitly set `OPENCLAW_RUN_NATIVE_WINDOWS_TTS=1`.
All three cases passed with zero skips:

| Test method in `WindowsTextToSpeechServiceTests` | Observed WAV byte counts |
|---|---|
| `NativeWindows_DefaultAndStaleConfiguredVoiceReturnSilentPcmWav` | 78,126 (default), 75,566 (stale configured voice) |
| `NativeWindows_UnreadyDefaultPiperUsesRealAdapterAndSilentOfflineFallback` | 90,926 |
| `NativeWindows_ExplicitInstalledVoiceWorksAndMissingVoiceStaysStrict` | 68,206 (installed voice); missing explicit voice throws without a second playback |

Each captured WAV passed RIFF/WAVE envelope and size, PCM format chunk,
nonempty data and nonzero sample assertions. Observed output was PCM16,
one channel, 16,000 Hz. Metadata only was recorded, not audio bytes.

These cases construct the real `TextToSpeechService` with its default
`WindowsTextToSpeechClient`, service-owned Windows callback, existing voice
resolution, native `SpeechSynthesizer` and native-to-managed copy.
**Only playback is replaced** by in-memory capture. Settings and Piper
directories are isolated fixtures; no downloaded Piper voice or real cloud key
is used. No microphone, audible output, cloud synthesis, voice installation
or model download occurred.

Other service tests replace synthesis and playback, except direct blank/long
input and terminal-disposal tests that retain the real adapter with a safe
callback. They establish forwarding and managed behavior, not native
empty/whitespace/long-input acceptance or actual MediaPlayer behavior.

## Failures and recovery, without erasing earlier results

Every command, candidate SHA, exit code and test inventory is retained in
`validation.json`. The sequence is:

| Commands | Observation and action |
|---|---|
| 01, 02 | `--no-restore` failed with NETSDK1004 because this fresh worktree had no assets. Normal restore/build with the session-only official NuGet v2 configuration then succeeded. |
| 04 | Production dependencies compiled, but WinUI packaging failed because the unrelated `wxc-exec.exe` was missing. The two harness flags do not suppress its separate copy target. |
| 05, 06 | Harness-only compilation exposed CS0313 in the new nullable `DurationMs` assertion. Commit `c6a6f4b8` uses `Assert.IsType<int>` before `Assert.InRange`. The next build compiled but could not copy 13 setup PNGs because the earlier WinUI build stopped before its copy targets. |
| 07, 08 | Invoked existing loose-setup-asset and VC-runtime targets for the exact x64 output, then built the test harness against already compiled production dependencies. No hand-copied DLLs or source/test weakening. |
| 09, 10 | Initial deterministic service 22/0/0 and silent native 3/0/0 passed. |
| 11 | Required full build hit the recorded npm TLS failure. No repeated endpoint probes. |
| 12, 13 | Rebuilt after the native opt-in attribute and terminal-disposal test; final service suite 26/0/0, native subset 3/0/0. |
| 14, 15, 17 | Shared built, then ran 4,106/1/32. `MxcConfigBuilderTests.Build_BootstrapsShellPathAndGrantsBackendSafePathDirsReadonly` depended on a direct-user ChangePermissions ACE absent from this task's TEMP. With explicit coordinating approval, verified ownership/non-reparse path and added only an inheritable current-user Allow FullControl ACE to that exact task-owned TEMP. Preserved other ACLs/inheritance, did not touch shared paths. The unchanged failing test then passed 1/0/0. Private ACL identity/path records are not published. |
| 16, 19, 20 | Tray ran 3,099/1/0: its native-runtime guard expected `bin\Debug`, not the previously repaired `bin\x64\Debug`. Invoked the existing VC target with Debug/win-x64 and **Platform omitted**, verified `TargetDir`, redist directory 14.44.35112 and actual runtime file 14.44.35211.0. The unchanged native loader guard then passed 1/0/0. |
| 18, 21 | Shared ran 4,106/1/32: `McpHttpServerTests.Dispose_DuringInFlightHandler_DoesNotSurfaceObjectDisposedException` observed closed-FileStream exceptions. The unchanged test passed 1/0/0 in isolation. This is an observed failure in an unrelated area, not a proven preexisting flake or an established root cause. |
| 22, 23 | Unchanged full suites reran: Shared 4,107/0/32; Tray 3,100/0/0. No tests were filtered out of either final suite. |

## Reproduction and harness qualifications

Use the normal prerequisite/setup and commands in the proposal first.
The ledger records all 23 exact executed commands, including failed attempts;
`$TtsRaw` denotes an isolated results directory, not a product setting.
Every test project was built before using `--no-build --no-restore`.

The successful scoped harness recovery after production compilation was:

```powershell
dotnet msbuild .\src\OpenClaw.Tray.WinUI\OpenClaw.Tray.WinUI.csproj -t:CopySetupAssetsLooseForConsumerCopy,CopyOpenClawVCRuntimeToOutput -p:Configuration=Debug -p:RuntimeIdentifier=win-x64 -p:Platform=x64 -getProperty:TargetDir,OpenClawVCRedistVersion -verbosity:minimal
dotnet build .\tests\OpenClaw.Tray.UITests\OpenClaw.Tray.UITests.csproj -c Debug -r win-x64 -p:Platform=x64 -p:BuildProjectReferences=false -p:SkipMxcNodeBridgeRestore=true -p:VerifyWxcExecShipped=false -p:UseSharedCompilation=false --no-restore --verbosity minimal
$env:OPENCLAW_RUN_NATIVE_WINDOWS_TTS = '1'
dotnet test .\tests\OpenClaw.Tray.UITests\OpenClaw.Tray.UITests.csproj -c Debug -r win-x64 -p:Platform=x64 --no-build --no-restore --filter 'FullyQualifiedName~WindowsTextToSpeechServiceTests' --logger 'trx;LogFileName=13-service-native-final.trx' --results-directory $TtsRaw
Remove-Item Env:\OPENCLAW_RUN_NATIVE_WINDOWS_TTS
```

`SkipMxcNodeBridgeRestore` avoids unrelated npm restore.
`VerifyWxcExecShipped=false` suppresses shipping verification, not its copy
target. `BuildProjectReferences=false` is only valid after the referenced
assemblies compiled successfully. It is not a recipe to skip unresolved
production compile errors. The two original flags alone did not build the
harness. This output is incomplete for shipping/deployment/sandbox purposes.

For the full Tray suite's separate default-platform output recovery:

```powershell
dotnet msbuild .\src\OpenClaw.Tray.WinUI\OpenClaw.Tray.WinUI.csproj -t:CopyOpenClawVCRuntimeToOutput -p:Configuration=Debug -p:RuntimeIdentifier=win-x64 -getProperty:TargetDir,OpenClawVCRedistVersion -verbosity:minimal
dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj -c Debug --no-build --no-restore --logger 'trx;LogFileName=22-shared-final.trx' --results-directory $TtsRaw
dotnet test .\tests\OpenClaw.Tray.Tests\OpenClaw.Tray.Tests.csproj -c Debug --no-build --no-restore --logger 'trx;LogFileName=23-tray-final.trx' --results-directory $TtsRaw
```

Do not copy either platform recipe blindly. Match `TargetDir` to the actual
test's expected output. Do not apply the task TEMP recovery to shared paths.

## Review, dependencies and remaining gaps

Read-only rubber-duck review found no blocking issue in the bounded seam.
Its native opt-in and terminal-disposal coverage suggestions were accepted
before the final service run. It explicitly did not establish actual playback
ordering or request a Piper lifetime rewrite. The bundled Codex autoreview
helper could not run: no Codex executable was available, and the discovered
Python command was a WindowsApps alias. No tooling was installed and no clean
Codex review is claimed.

The actual WinUI restored graphs select MEAI Abstractions 10.9.0's native
`net10.0` assembly, with no dependencies on that target. Existing Whisper.net
1.9.0 requires Abstractions >=10.0.0; Whisper runtime 1.9.1 and Sherpa 1.13.0
are unchanged. The API still reports experimental diagnostic `MEAI001`.
No ML.NET, MEAI middleware, telemetry exporter or new inference engine is added.
ARM64 resolution is metadata evidence only, not ARM64 execution. The independent
STT source is not imported; the combined-branch behavior is untested.

No claim is made for actual MediaPlayer stream playback, queue/interruption
ordering, audible/UI behavior, MCP/gateway invocation, native blank/long text,
all voices/languages, native cancellation latency, ARM64, Piper/cloud inference,
full duplex, allocation equivalence or performance. The complete WAV requires
extra buffering/copies. The adapter still receives its Windows synthesis
callback from the service; it is not a standalone reusable Windows engine.

Future product-path proof would need the `windows-winui-interactive` pool
and, for gateway-mediated invocation, `windows-wsl-gateway-e2e`. Neither pool
was exercised here. Audible playback and credential access were outside this
validation's authorization. A frozen off-host workflow was prepared but not
pushed or executed after the workflow-auth blocker; it is not evidence.

Public evidence is an allowlisted projection, not raw TRX or raw logs.
It includes hashes of the retained raw TRX, commands/exit codes, complete
counts, selected method inventories and numeric WAV metadata. It excludes
machine paths, hostnames, account identities, arbitrary test output, settings,
credentials, speech text and audio. The raw inputs remain local.
