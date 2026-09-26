# Whisper speech-interface proposal evidence

This is a Windows x64 implementation pilot, not a merge-ready PR or a successful
full application build. No performance, accuracy, Talk Mode, microphone, or live
UI improvement is claimed.

## Source identity

| Revision | Meaning |
|---|---|
| `273b0182745a3093c0e09f306ca8a1fff6ef3c5a` | Independent baseline, including the original concrete Whisper service. |
| `9b338475df8d51a36bca92f6355d74ab147a463c` | Separate async-lifecycle prerequisite and awaited `VoiceService` callers. |
| `15134f936c2ede2f1d6bd28ddc1395797f3d0d3e` | MEAI consumption, deterministic tests, opt-in native proof, and usage documentation. |
| `5a59535216eea603192f2df13d4eddf1e1a20267` | Upstream default-branch HEAD observed again at closeout. Not the tested base. |

The three upstream commits since the base concern setup restart handoff, LF-safe
tray source tests, and Inno-to-Store migration. They do not change
`SpeechToTextService`, `VoiceService`, or the Shared package reference. They do
change other app/test surfaces, so these results are not whole-current-main
validation.

Tests executed before the two source commits were created. The final tested
source Git blobs were checked against the committed tree, without changing their
content. [validation.json](validation.json) records those blobs, file SHA256s,
the actual compiled assemblies' versions/SHA256s, restored package identities,
TRX-derived counts, every new focused case, all 33 ordinary skips, and loaded
native-module hashes. The intermediate lifecycle commit was not separately
built or tested; acceptance evidence belongs to the combined source.

[initial-source.patch](initial-source.patch) preserves the initial tracked tree
against the baseline, including service blob
`e774c5b576ff67914037aec2962fd1558609fe00`. It identifies the older candidate used
by the initial 131-case focused run, first native comparison, and failed full
build. Those results are not relabeled as final-source evidence. Complete early
untracked test snapshots and early binary hashes were not retained; final
evidence supersedes them. The final service blob is
`288c86363f959208852654d11a52602170302dbc`.

## Package and contract evidence

Whisper.net 1.9.0 already brings Microsoft.Extensions.AI.Abstractions >=10.0.0
transitively. This pilot **consumes the standard speech interface**, rather than
introducing MEAI to a previously MEAI-free dependency graph.

The actual restored Shared, WinUI, Shared.Tests, Tray.Tests, and WinNode.Cli.Tests
graphs all resolve Whisper.net 1.9.0, Whisper.net.Runtime 1.9.1, and Abstractions
10.9.0. The native proof loaded Whisper assembly 1.9.0.0 and MEAI assembly
10.9.0.0 successfully. The stable Abstractions package's speech interfaces are
still **experimental (`MEAI001`)**. Warning suppression is scoped to the service
and its two direct speech-API test files, not the project.

Primary references:

- [Shipped adapter source at the 1.9.0 package commit](https://github.com/sandrohanea/whisper.net/blob/094615b303e4db907ea2218c18e3965f5c9acbb2/Whisper.net/SpeechToTextClient/WhisperSpeechToTextClient.cs).
- [Whisper.net 1.9.0 package](https://www.nuget.org/packages/Whisper.net/1.9.0).
- [Abstractions 10.9.0 package](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions/10.9.0).
- [Immutable MEAI 10.9.0 source revision](https://github.com/dotnet/extensions/tree/b10f9c0a081b5dbb7755b8f5592e1d3c3f550a3a).
- [Prior-art integration discussion](https://github.com/luisquintanilla/mlnet-audio-custom-transforms/blob/main/docs/meai-integration.md), not an imported runtime or model dependency.

The pinned adapter emits a complete Whisper segment in each streaming update,
despite its `TextUpdating` kind. The service maps those updates, not flattened
`response.Text`. Other providers might have different update semantics; this is
not a provider-neutral delta assembler.

`SpeechLanguage` is the existing normalized two-letter language or `auto`.
`SpeechSampleRate=16000` describes the WAV; the real input header, not the option
alone, enforces 16 kHz mono PCM16. Threads remain `max(1, CPU count / 2)`, eight on
this host. `TextLanguage` is deliberately unset because the pinned adapter
enables translation for any nonempty target language. Returned segment language
still means normalized requested language, including `auto`, not detected
language. Segment order, trimmed nonempty text, and exact timestamps are retained.

The pinned `GetService` throws `NotImplementedException`. The service never
calls it. Native observation records the limitation without making continued
failure an acceptance requirement. Broader metadata/middleware reuse needs a
provider fix; this proposal does not duplicate the adapter.

## Ownership prerequisite and intentional behavior change

Previously, only transcriptions acquired the semaphore. A concurrent synchronous
load, unload, or disposal could free the factory while a processor used it.
Simply making those synchronous methods wait would introduce a long UI stall:
`VoiceService.InitializeAsync` is reached from the voice overlay and
`VoiceService.DisposeAsync` from settings unload/shutdown.

The prerequisite retains the existing gate and adds async lifecycle admission.
Both owned production lifecycle calls await it. Capture, VAD, drain timeout,
agent-local pipelines, and shutdown orchestration are unchanged.

| Interleaving | Original behavior | Combined proposal |
|---|---|---|
| Inference owns model; UI requests load/unload/disposal | Native lifetime could overlap, or a naive gate fix could block UI synchronously. | Returns a pending task; native mutation waits for enumeration/processor cleanup. |
| Inference owns model; legacy sync lifecycle call | Could mutate/free active model. | Immediately throws the documented busy error before mutation. |
| Queued async load/unload is canceled | No async admission API. | Cancellation leaves current ownership/readiness intact. |
| Eager model loaded but no inference occurred | Service owned its concrete factory. | Service retains eager ownership even before the lazy adapter adopts it. |
| Client creation fails | No adapter creation step. | Owned model is cleaned; primary creation error preserved. |
| Client disposal and model cleanup both fail | No two-owner seam. | Model cleanup attempted; secondary failure logged without masking the primary client error. |

Synchronous APIs preserve idle use, not unrestricted concurrency or use after
disposal. Busy calls now fail fast; post-disposal operations reject use.
`Dispose`/`DisposeAsync` are idempotent. The adapter and service can both call the
pinned factory's idempotent disposal, while its native resource is freed once.

`ForceYielding` moves even uncontended admission off the caller synchronization
context. Derived-context fake tests prove the service boundary does not require
a UI continuation. They do **not** prove visible WinUI responsiveness.
Async disposal can await the rest of an uncanceled native inference and has no
new timeout. This is **not a bounded shutdown guarantee**.

## Final executable evidence

Platform: SDK-style .NET 10, VSTest mode executing VSTest, xUnit 2.9.3, Windows
x64, Debug, SDK 10.0.401.

| Check | Total | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| Focused STT/lifecycle/language/capability/MCP contracts | 136 | 136 | 136 | 0 | 0 |
| Opted-in native comparison | 1 | 1 | 1 | 0 | 0 |
| Full Shared suite | 4171 | 4138 | 4138 | 0 | 33 |
| Full Tray suite | 3072 | 3072 | 3072 | 0 | 0 |
| Full WinNode CLI suite | 127 | 127 | 127 | 0 | 0 |

All five final runner invocations exited zero. Counts were parsed from actual
TRX cases/counters; missing, malformed, empty, unsuccessful, or mismatched reports
were rejected. Focused/native rows overlap the full suite and must not be added
as unique coverage.

Shared's 33 skips are 16 DeviceIdentity, nine LocalCommandRunner, and seven MXC
integration cases gated by `OPENCLAW_RUN_INTEGRATION`, plus the one separately
opt-in Whisper native case. Native opt-in variables were cleared before ordinary
suites. The 33 skips are not passes or native coverage.

Representative requirement-to-test mapping (full case inventory in JSON):

| Requirement | Evidence |
|---|---|
| Same input conversion/model/options; language normalization/auto; nonempty trimming; segment ordering/times | `Transcribe_PreservesPcmBytesOptionsAndOrderedSegments`, plus existing normalization tests. Fixed expected PCM/WAV bytes are independent of candidate helpers. |
| Cancellation including no success-shaped partial fallback | `Transcribe_CanceledIteratorCannotReturnPartialSuccess`, both iterator-stop and late-update cases; `Transcribe_CancellationRemainsCapabilityCancellation`. |
| Errors remain sanitized | `Transcribe_ProviderFailureIsNotPartialSuccessAndCapabilitySanitizesIt`. |
| Single-flight/model reuse | `Transcribe_SerializesCallsAndCancelsQueuedCallWithoutReplacingClient`. |
| UI-context callers remain responsive during contended lifecycle operations; no native disposal until active work finishes | `ModelLifetime_AsyncUiCallerRemainsResponsiveUntilEnumerationEnds`, unload/reload/dispose cases with controlled fake enumeration and model/client disposal assertions. |
| Sync busy calls fail fast before native mutation | `ModelLifetime_SynchronousBusyCallFailsWithoutChangingOwnership`, load/unload/dispose cases. |
| Correct reload/unload/cancellation | `ModelLifetime_QueuedCancellationLeavesCurrentModelOwned`, `ModelLifetime_ReadinessTracksExplicitLoadFailureUnloadAndDispose`. |
| Exactly-once cleanup, including client creation/disposal errors | `ModelLifetime_DisposesUnusedLoadedClientExactlyOnce`, `ModelLifetime_ModelCleanupFollowsClientEvenIfClientDisposalThrows`, `ModelLifetime_ClientCreationFailureCleansOwnedModelAndPreservesPrimaryError`, `ModelLifetime_TwoDisposalFailuresPreserveClientFailure`. |
| Off-caller context even when admission is uncontended | `Transcribe_DoesNotEnterAdapterOnCallerSynchronizationContext`, `ModelLifetime_AsyncLoadAndRepeatedDisposalStayOffCallerContext`. |
| Real call-site awaiting | `VoiceService_AwaitsModelLoadAndDisposal` source guard, plus actual WinUI compile below. |
| Caller-owned streams and same native transcription | `Native_ServiceMatchesFrozenBaselineAndPreservesOwnership`. |

### Native asset and backend identity

Only two approved public assets were downloaded, into an ignored task cache:

| Asset | Source identity | Size / hash |
|---|---|---|
| `ggml-tiny.bin` | `ggerganov/whisper.cpp`, revision `5359861c739e955e79d9a303bcbc70fb988958b1`, MIT model card | 77,691,713 bytes; SHA256 `be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21` |
| `samples/jfk.wav` | `ggml-org/whisper.cpp`, revision `d09f61a708f3487afa956ff578e60eae5e7a233c`, public presidential-speech sample in the MIT repository | 352,078 bytes; Git blob `3184d372cd2f8b804d3a540c70ec50d927b335d2`; SHA256 `59dfb9a4acb36fe2a2affc14bacbee2920ff435cb13cc314a08c13f66ba7860e` |

The fixture Git blob includes the `blob <length>\0` header, not raw-file SHA1.
The proof supplies the same 176,000 fixed float samples (11 seconds) to both
paths. Float-byte SHA256:
`ebd52851100536db02d12c49fddd010372dcdc70243562e057553d476b706ae0`.

Actual `RuntimeOptions.LoadedLibrary` was `Cpu`; native runtime reported AVX2,
F16C, FMA, BMI2 and OpenMP support. The test enumerated **loaded process modules**,
not merely files in an output directory. JSON records SHA256s for
`whisper.dll`, `ggml-whisper.dll`, `ggml-base-whisper.dll`, and
`ggml-cpu-whisper.dll`.

The baseline independently freezes the original 273b018 processor, PCM
conversion, and segment mapping. It does not call candidate helpers. Both paths
use the same model/runtime, input, language and threads, without translation.
Each hash covers the complete ordered segment text, start/end ticks and language:

| Requested / normalized language | Repeat | Baseline and candidate SHA256 (equal) | Segments each |
|---|---:|---|---:|
| `en-US` / `en` | 1 | `ebec48e0cacf88c1fb0bd913e4ea1bb18569ca219e68ce84dad0fe16f2e99a0c` | 1 |
| `en-US` / `en` | 2 | `ebec48e0cacf88c1fb0bd913e4ea1bb18569ca219e68ce84dad0fe16f2e99a0c` | 1 |
| `auto` / `auto` | 1 | `8bc1d3093b9c0614e143c2f3256bfb80ab6e73335a0e3d62bdcbdd75748144df` | 1 |
| `auto` / `auto` | 2 | `8bc1d3093b9c0614e143c2f3256bfb80ab6e73335a0e3d62bdcbdd75748144df` | 1 |

Every pair and repeat matched exactly; no tolerance or nondeterminism allowance
was added. One additional pinned-adapter call proved the caller's WAV stream
remained open. Native pre-canceled admission asserted the actual cancellation
token. Output explicitly records
`cancellationScope=preCanceledAdmissionOnly; nativeMidFlightAbort=unverified`.
No unsafe native disposal experiment or wall-clock cancellation deadline ran.
Recorded durations are diagnostics, not benchmark results.

The fixture yielded only one segment per call. Multi-segment ordering/trimming
is covered by deterministic fakes, not a diverse native speech corpus.

### Build failures and limited recovery

The one required `.\build.ps1 -NoTrustRepository` attempt failed fetching locked
`node-pty-1.2.0-beta.12.tgz` from npm:
`ERR_SSL_SSL/TLS_ALERT_HANDSHAKE_FAILURE`. It was not retried or bypassed.
Documentation validation passed 50 files; Shared, Cli, WinNodeCli and SetupEngine
built, but full WinUI packaging did not.

A scoped WinUI build with `SkipMxcNodeBridgeRestore=true` compiled the real
changed caller and produced its assembly, then failed copying absent
`wxc-exec.exe`. The existing targets below subsequently exited zero:

```powershell
dotnet msbuild .\src\OpenClaw.Tray.WinUI\OpenClaw.Tray.WinUI.csproj `
  "-t:Compile,CopyOpenClawVCRuntimeToOutput,CopySetupAssetsLooseForConsumerCopy,CopySetupDefaultConfigToOutput" `
  -p:RuntimeIdentifier=win-x64 -p:Configuration=Debug `
  -p:SkipMxcNodeBridgeRestore=true -p:UseSharedCompilation=false -m:1 -v:minimal
```

`Platform` was omitted. The inspected actual target was
`bin\Debug\net10.0-windows10.0.22621.0\win-x64`, not `bin\x64\Debug`.
This establishes scoped compilation and test harness assets only, not a
successful full build, shipped MXC bridge, app launch, or interactive UI proof.

A test-helper nesting/accessibility error (`CS0053`) during proof strengthening
was fixed and all final suites rerun. No test was skipped or weakened to hide it.
Read-only rubber-duck review prompted the stronger derived-context and explicit
owned-model cleanup tests; it did not establish untested application behavior.

## Reproduce on an approved Windows host

Keep the same engine/packages and use an isolated worktree/settings directory.
The executed commands, including exact filters and TRX filenames, are in JSON.
The sequence was:

```powershell
$env:OPENCLAW_REPO_ROOT = (Get-Location).Path
$env:OPENCLAW_TRAY_DATA_DIR = '<isolated-settings-directory>'
dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~SpeechToTextServiceTests|FullyQualifiedName~SpeechToTextLifecycleContractTests|FullyQualifiedName~SpeechToTextLanguageNormalizationTests|FullyQualifiedName~SttCapabilityTests|FullyQualifiedName~McpToolBridgeTests" `
  -m:1 -p:UseSharedCompilation=false --logger "trx;LogFileName=stt-focused-final.trx"
try {
    $env:OPENCLAW_RUN_WHISPER_PROOF = '1'
    $env:OPENCLAW_WHISPER_PROOF_ASSETS = '<verified-public-assets-directory>'
    dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj --no-build --no-restore `
      --filter FullyQualifiedName~SpeechToTextNativeProofTests --logger "trx;LogFileName=stt-native-final.trx"
}
finally {
    Remove-Item Env:OPENCLAW_RUN_WHISPER_PROOF,Env:OPENCLAW_WHISPER_PROOF_ASSETS -ErrorAction SilentlyContinue
}
dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj --no-build --no-restore
dotnet test .\tests\OpenClaw.Tray.Tests\OpenClaw.Tray.Tests.csproj --no-build --no-restore
dotnet test .\tests\OpenClaw.WinNode.Cli.Tests\OpenClaw.WinNode.Cli.Tests.csproj --no-build --no-restore
```

Before `--no-build --no-restore`, build/restore the corresponding unchanged
project/configuration. Initial Tray and CLI runs did so and passed. Fresh
worktree `--no-restore` no-ops do not count as proof.

Environment recovery used process-local paths/settings/TEMP, a session-only
official NuGet v2 configuration, and existing VS2022 Enterprise VC-runtime copy
targets. The task TEMP needed an additive inheritable direct-current-user ACE;
ownership/non-reparse checks preceded that narrowly authorized change. No
global configuration, shared ACL, TLS bypass, account change, mirror, or lockfile
change was made.

## Remaining proof and boundaries

For a future PR, schedule `windows-winui-interactive` for real lifecycle UI
behavior and `windows-11-arm64` for native architecture parity. Both remain
unverified here. MCP/capability/CLI contract tests pass, but no live MCP/gateway
round trip or private microphone recording was run. `stt.transcribe`,
`stt.listen`, and `stt.status` schemas, bounded capture, VAD, privacy-safe errors,
engine labels, permissions, downloads, model catalog, and gateway transport were
not changed.

No new telemetry, middleware, ML.NET, ONNX model, cloud provider, engine
substitution, or generic lifetime framework was added. The pilot does not claim
to resolve adjacent Talk Mode, capture, drain, or interruption features.

Public artifacts contain source patches and bounded hashes/metadata only.
Model/audio binaries, transcript payloads, raw TRX/logs, private paths, user
settings, credentials, and raw ACL/SID details remain unpublished. Final heavy
work ended before the host slot was released at 06:18:44 -04:00; subsequent
source/evidence commits did not run further builds or native work.
