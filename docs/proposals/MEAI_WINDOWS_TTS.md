# Windows synthesis through Microsoft.Extensions.AI

This is a Windows-only provider-boundary pilot, not a four-provider migration
or a merge-ready contribution. It makes the existing Windows synthesis path
consumable as `ITextToSpeechClient` and lets tests replace synthesis without
replacing the actual `TextToSpeechService` selection and completion logic.
Piper, ElevenLabs and MiniMax keep their existing implementations.

## Boundary and ownership

```text
tts.speak / existing application callers
  -> TextToSpeechService
     selection, readiness, offline fallback, configured/explicit voice policy
       -> ITextToSpeechClient.GetAudioAsync (Windows branch only)
          -> WindowsTextToSpeechClient
             -> existing Windows SpeechSynthesizer, one per request
          <- TextToSpeechResponse with one DataContent, audio/wav
       -> existing MediaPlayer playback, gate, interruption and completion
  <- existing spoken/provider/requestedProvider/fellBack/contentType/durationMs
```

The adapter receives the Windows synthesis callback from the existing service.
This deliberately keeps configured versus explicit voice selection in OpenClaw,
not in the general MEAI options. Existing DI continues to own
`TextToSpeechService`; no global or competing `ITextToSpeechClient` registration
is introduced. This callback is a small composition seam, not a standalone
reusable Windows provider: other consumers would still need to supply its
Windows synthesis/voice-policy callback. An injected fake client does not run
that configured-voice policy; the native tests retain the callback to cover it.

The service owns the adapter, including an internally injected test client.
Each native invocation owns and disposes its synthesizer, synthesis stream and
reader before returning a managed WAV. The response owns that managed buffer;
it needs no disposable stream and contains no native handle. OpenClaw copies
the response into its existing in-memory playback stream. This adds buffering
and copies compared with playing the original Windows stream directly.
No allocation or latency improvement is claimed.

Concurrent calls use separate native synthesizers, just as before. Disposing
the adapter rejects new calls without synchronously waiting on the UI thread
or destroying another call's native resources. Accepted calls finish or cancel
through their own tokens and `using` scopes. The adapter does not own the
service or the callback's captured settings.
Disposal is terminal for synthesis even though the existing readiness snapshot
describes configured provider availability rather than service lifetime.

Cancellation before synthesis prevents the callback. Cancellation during the
Windows async operation is forwarded to its WinRT task. Cancellation after it
returns prevents publishing a result or beginning playback. This is not a
promise that Windows immediately aborts native work. Playback has its existing,
separate cancellation registration. `tts.speak` still completes after playback,
not after synthesis, and `durationMs` still measures both.

## Supported options and audio

| MEAI option | Pilot behavior |
|---|---|
| `VoiceId` | Passed unchanged to OpenClaw's existing Windows voice policy. ID and display-name matching remain case-insensitive, with trimming at resolution. |
| `AudioFormat` | Omitted, `wav` or `audio/wav` (case-insensitive). Other formats fail. |
| `ModelId`, `Language`, `Speed`, `Pitch`, `Volume` | Non-null values fail with `NotSupportedException`, including apparently neutral values. The existing voice-only path does not apply these controls. |
| `RawRepresentationFactory`, nonempty `AdditionalProperties` | Fail without invoking the callback. |

Options are not mutated. The node API is unchanged: in particular this does not
add language/speed options or reinterpret the existing provider-specific `model`
argument for Windows. Windows language and speaking defaults still come from
the selected OS voice.

Null text fails before native synthesis, as required by the MEAI contract.
All non-null strings, including empty and whitespace-only strings, pass through
without trimming or length caps. The node capability retains its original
nonblank validation, text normalization and 5,000-character limit at that boundary.
Direct Windows service/client calls retain forwarding of longer or blank text,
subject to the existing Windows backend's own constraints. The fake regression
tests prove forwarding, not native acceptance of those inputs.

The response contains the complete, unmodified RIFF/WAVE returned by Windows,
with its actual format chunk, sample rate and channels. There is no resampling
or invented fixed sample rate. The adapter performs a RIFF/WAVE envelope check;
it is not a general-purpose audio decoder. Native proof additionally checks the
PCM format, rate, channels, bit depth, RIFF size and nonzero sample data.

`GetStreamingAudioAsync` is a compatibility operation that yields exactly one
`AudioUpdated` update after the entire WAV exists. It is not incremental speech,
low-latency streaming, conversational turn-taking or full-duplex audio.

`GetService` returns the adapter or `TextToSpeechClientMetadata` for unkeyed
matching types, null for unsupported/keyed requests, and throws for a null
type. Metadata reports `windows` with no invented server URI or model ID.
No synthesis text, audio or native object is copied into `RawRepresentation`.

## Package evidence and compatibility

- Verified pin: `Microsoft.Extensions.AI.Abstractions` **10.9.0**, in the tray
  project and its source-linked pure test project. No `Microsoft.Extensions.AI`
  middleware, ML.NET or additional engine is added.
- [Official NuGet metadata](https://www.nuget.org/api/v2/Packages(Id='Microsoft.Extensions.AI.Abstractions',Version='10.9.0'))
  reports a stable package published 2026-08-11, with a native `net10.0` asset
  group and no dependencies for that group. Other target groups depend on
  `System.Text.Json` 10.0.11.
- [Release source v10.9.0](https://github.com/dotnet/extensions/tree/b10f9c0a081b5dbb7755b8f5592e1d3c3f550a3a/src/Libraries/Microsoft.Extensions.AI.Abstractions/TextToSpeech)
  defines `GetAudioAsync`, `GetStreamingAudioAsync` and `GetService`.
  The speech API is **experimental**, despite the stable package version.
  `MEAI001` suppression is confined to files using this API, not a repository
  or project-wide diagnostic suppression.
- Existing Whisper.net 1.9.0 already brings MEAI Abstractions >=10.0.0 into the
  dependency graph. This is an explicit compatible-version selection, not a
  claim that MEAI is a wholly new dependency. The independent STT proposal uses
  the same 10.9.0 pin. The actual restored WinUI graph selects the `net10.0`
  Abstractions asset with no added dependencies in the neutral, `win-x64` and
  `win-arm64` graphs. This branch compiled and ran the Windows path on x64;
  combining the independent STT branch and running ARM64 remain unverified.
- Prior art: [ML.NET audio MEAI integration](https://github.com/luisquintanilla/mlnet-audio-custom-transforms/blob/main/docs/meai-integration.md).
  Its engine and middleware are not imported. Its diagnostic/version guidance
  is not substituted for the exact release source above.

There is no new logging or OpenTelemetry middleware. Ambient sensitive-data
flags cannot enable nonexistent MEAI telemetry here. Existing OpenClaw error
sanitization remains the node boundary; direct adapter callers receive the
existing exceptions and must not export their contents indiscriminately.

## Why stop at Windows?

The native Piper model is lazily reused, unlike the per-request Windows object.
The baseline releases `_piperLock` after `AcquirePiperClient`, before generation.
A concurrent voice change or service disposal can call the old client's
`Dispose` while another request is generating. This is a reported source-level
interleaving, not a reproduced native failure, an interface-contract violation
or a fix included here.

A Piper follow-on needs independently reviewed model lifetime and voice-switch
ownership. Its deterministic tests should hold generation open, request a
different voice, cancel a waiter, dispose the owner and then release generation.
They must prove single-flight use, deferred safe cleanup, no result published
after cancellation and no synchronous UI wait. Native offline fixtures would
then verify actual PCM output and model reuse. None of that is demonstrated by
this Windows pilot. There is no automatic cloud fallback.

Keeping the current implementation is a reasonable alternative if a reusable
synthesis contract and service-path injection do not justify experimental API
coupling and the extra Windows buffering. A private OpenClaw-specific interface
would avoid that experimental dependency but would not be interoperable with
MEAI consumers. Replacing Piper with an unrelated engine merely to obtain an
interface is not proposed.

## Validation and reproduction

Validated locally on Windows x64 with SDK 10.0.401 and runtime 10.0.12 at
source `2c4ec89c2196cccce484b97e29cf54d6c5804d7a`.
The [evidence report](evidence/meai-windows-tts/README.md) and
[allowlisted command/test ledger](evidence/meai-windows-tts/validation.json)
preserve the commands, original UTC timestamps, candidate SHAs, failures,
recoveries, package graph, hashes and selected method inventories.

| Check | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Focused adapter plus existing Piper/cloud contract tests | 46 | 0 | 0 |
| Final actual-service suite, including the three native cases | 26 | 0 | 0 |
| Final full Shared suite | 4,107 | 0 | 32 |
| Final full Tray suite | 3,100 | 0 | 0 |

The three explicitly enabled native cases passed through the actual service,
default adapter, existing Windows voice resolver and native synthesis with only
playback replaced. They produced four nonempty PCM16 mono WAVs at 16,000 Hz.
This is observed output on one host, not a fixed-format promise for other voices.
Counts overlap; focused results must not be added to the full Tray total.

**Full build remains blocked.** The one required `build.ps1` attempt failed
fetching the locked `node-pty` package over TLS. Scoped WinUI harness compilation
is not a successful full build. Initial environment/test failures and the
one source-level test compilation fix are retained in the evidence report.

Baseline expectations come from
[`TextToSpeechService` at 273b018](https://github.com/openclaw/openclaw-windows-node/blob/273b0182745a3093c0e09f306ca8a1fff6ef3c5a/src/OpenClaw.Tray.WinUI/Services/TextToSpeech/TextToSpeechService.cs)
and
[`TtsCapability` at 273b018](https://github.com/openclaw/openclaw-windows-node/blob/273b0182745a3093c0e09f306ca8a1fff6ef3c5a/src/OpenClaw.Shared/Capabilities/TtsCapability.cs),
independently of the new adapter:

| Baseline expectation | Pilot expectation |
|---|---|
| Explicit Windows voice ID or display name must resolve; stale configured voice uses the OS default. | Same native resolver and policy. Silent native tests cover installed ID, missing explicit ID and stale configured ID. |
| Node text is trimmed, nonblank and bounded to 5,000 characters; direct Windows service calls forward text without those managed guards. | Node behavior unchanged. Direct adapter and actual-service tests forward empty, whitespace-only and 5,001-character strings to a safe synthesis fake without trimming or rejection. Native blank/long-input acceptance is not claimed. |
| Unready configured provider falls back to Windows; explicit provider is strict; fallback drops provider-specific voice/model. | Same resolver, ready-set and argument rewrite. Fake service tests cover each provider; silent native proof also exercises unready default Piper through the real adapter. |
| Cancellation is passed to Windows synthesis and independently to MediaPlayer playback. | Same tokens at both stages, plus post-synthesis checks before exposing audio. No stronger native-abort promise. |
| Per-call synthesizer and source stream remain alive until playback returns, then are disposed. | Per-call synthesizer/source stream/reader are disposed after copying the WAV. A separate playback stream stays alive until playback returns. This lifetime change and the extra copies are intentional. |
| Playback gating, interruption and completion are owned by OpenClaw. | Same playback code; fake tests verify awaiting and token/interrupt forwarding, not live MediaPlayer ordering. |

On an isolated Windows worktree with the normal repository prerequisites:

```powershell
$env:OPENCLAW_REPO_ROOT = (Get-Location).Path
$env:OPENCLAW_TRAY_DATA_DIR = Join-Path (Get-Location) 'artifacts\tts-test-settings'
dotnet build .\tests\OpenClaw.Tray.Tests\OpenClaw.Tray.Tests.csproj
dotnet test .\tests\OpenClaw.Tray.Tests\OpenClaw.Tray.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~WindowsTextToSpeechClientTests
dotnet build .\tests\OpenClaw.Tray.UITests\OpenClaw.Tray.UITests.csproj -r win-x64 -p:Platform=x64
$env:OPENCLAW_RUN_NATIVE_WINDOWS_TTS = '1'
dotnet test .\tests\OpenClaw.Tray.UITests\OpenClaw.Tray.UITests.csproj --no-build --no-restore -r win-x64 -p:Platform=x64 --filter FullyQualifiedName~WindowsTextToSpeechServiceTests
Remove-Item Env:\OPENCLAW_RUN_NATIVE_WINDOWS_TTS
.\build.ps1 -NoTrustRepository
dotnet build .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj
dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj --no-build --no-restore
dotnet test .\tests\OpenClaw.Tray.Tests\OpenClaw.Tray.Tests.csproj --no-build --no-restore
```

Build/restore fresh test projects before using `--no-restore`, and verify a
nonzero test count. The `NativeWindowsSpeech` category synthesizes through the
actual service and installed OS voice, but replaces playback with an in-memory
capture. It must not play sound or download a model. Tests use temporary
settings and Piper directories and do not read real API keys. The three native
cases require `OPENCLAW_RUN_NATIVE_WINDOWS_TTS=1`; ordinary CI may lack voices
and skips them without that explicit opt-in. After opt-in, unavailable or broken
native speech fails the tests, rather than becoming a passing or skipped proof.
Only a run with all three cases passed and zero skipped counts as native proof.
These native
cases retain the production service, its default client construction, the real
adapter, voice resolution, OS synthesis and WAV copy. Only playback is replaced.
Other service cases replace both the synthesis client and playback, except the
blank/long-text and terminal-disposal regressions, which keep the real adapter
with a safe synthesis callback.

The full-build attempt reproduced the reported host TLS failure downloading
`node-pty-1.2.0-beta.12.tgz`. It was not retried. No TLS, lockfile, global
configuration or credential workaround was used.

If that baseline npm blocker prevents building the focused test harness,
`-p:SkipMxcNodeBridgeRestore=true -p:VerifyWxcExecShipped=false` may be used only
for the explicitly labeled TTS harness build. The first avoids the unrelated
MXC npm restore; the second disables the shipping verification target, but does
not disable the separate `CopyWxcExecToOutput` target. These two flags alone did
not yield a successful harness build. The evidence report records compilation
of the production dependencies, existing loose-asset/runtime copy targets and
the subsequent harness-only `BuildProjectReferences=false` build. None of these
steps proves shipping-output completeness, sandbox behavior, full build
correctness or deployment.

| Requirement | Named evidence in the recorded runs |
|---|---|
| "data-content/container checks" | `GetAudioAsync_MapsVoiceAndReturnsOwnedPcmWav`, `NativeWindows_DefaultAndStaleConfiguredVoiceReturnSilentPcmWav` |
| "selection/fallback/explicit-provider failure" | `SpeakAsync_UnreadyConfiguredProviderFallsBackOfflineAndDropsProviderSpecificOptions`, `SpeakAsync_ExplicitUnavailableProviderNeverCallsWindowsOrPlayback` |
| "malformed/null/empty input" | `GetAudioAsync_NullTextFailsBeforeSynthesis`, `GetAudioAsync_DirectCallsForwardEmptyAndWhitespaceText`, `SpeakAsync_DirectWindowsPathForwardsEmptyAndWhitespaceText`, `SpeakAsync_RejectsMalformedSynthesisResponseBeforePlayback`; existing `Speak_ReturnsError_WhenTextMissing` retains node-only nonblank validation. |
| "text limits" | `GetAudioAsync_DirectCallsPreserveTextLongerThanNodeLimit`, `SpeakAsync_DirectWindowsPathPreservesTextLongerThanNodeLimit`; existing `Speak_ReturnsError_WhenTextTooLong` retains the node-only guard. |
| "option mapping" | `GetAudioAsync_MapsVoiceAndReturnsOwnedPcmWav`, `GetAudioAsync_UnsupportedOptionsFailBeforeSynthesis` |
| "cancellation before/during/after synthesis" | `GetAudioAsync_AlreadyCanceledDoesNotStartSynthesis`, `GetAudioAsync_CancellationDuringSynthesisPropagatesAndReleasesOperation`, `GetAudioAsync_CancellationAfterBackendCompletionDoesNotPublishAudio` |
| "playback cancellation and disposal/model lifetime" | `SpeakAsync_PlaybackCancellationDoesNotReportSpokenOrDisposeClient`, `Dispose_RejectsNewCallsWithoutBlockingOrReleasingActiveOperations`, `Dispose_ServiceOwnsInjectedClient`. Piper model lifetime remains outside this pilot. |
| "tts.speak means synthesize AND play" | `SpeakAsync_WindowsAwaitsPlaybackAndPreservesCompletionMetadata` uses captured fake playback; actual MediaPlayer proof remains unverified. |
| "GetService metadata/type discovery" | `GetService_ReturnsOnlyUnkeyedMetadataAndAdapter` |
| "do not label that genuinely incremental low-latency streaming" | `GetStreamingAudioAsync_YieldsOneCompletedWaveNotIncrementalChunks` |

Actual MediaPlayer queue ordering, interruption, audible/UI behavior, MCP and
gateway invocation, ARM64, all installed voices/languages, OS-level cancellation
timing, and Piper/cloud engines remain unverified. The playback implementation
is unchanged, but unchanged source is not live behavior proof.

## Bounded implementation and acceptance

1. Pin Abstractions 10.9.0 explicitly where its types are compiled. Confirm the
   native `net10.0` graph and existing Whisper compatibility.
2. Add the Windows adapter and its narrow option/error/ownership contract.
   Do not change native engines, credentials, provider readiness or node schemas.
3. Separate the existing Windows synthesis method's completed WAV from
   playback. Pass it through `ITextToSpeechClient` and retain OpenClaw's
   MediaPlayer gate, interruption, fallback and completion metadata.
4. Characterize the service with safe injected clients/playback, then exercise
   silent native Windows synthesis. Run the mandatory repository checks.
5. Review the buffering cost and experimental API risk before deciding whether
   this one-provider seam merits adoption. Address native/UI/MCP/gateway proof
   gaps before presenting a future PR as ready.

Acceptance is Windows-only: real Windows WAV generation through the interface,
strict explicit provider/voice failures, offline fallback, preserved completion
semantics, deterministic lifecycle tests, a confirmed package graph and honest
full-build/proof status. Four-provider interchangeability and a Piper migration
are not acceptance claims for this proposal.
