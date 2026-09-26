# Managed audio arithmetic experiment

Status: meter plus gain passed the initial and final three-run performance gates
on the available native Windows x64 host. Original scalar VAD is retained.
The full build remains blocked by npm TLS; this is a discussion proposal,
not a merge-ready change. See
[openclaw/openclaw-windows-node#1524](https://github.com/openclaw/openclaw-windows-node/issues/1524).
No application, transcription, battery, or cross-architecture speedup is claimed.

## Reproduction

```powershell
dotnet run -c Release --project .\benchmarks\OpenClaw.Audio.Benchmarks\OpenClaw.Audio.Benchmarks.csproj -- --filter "*"
```

Use `--smoke` for an out-of-process dry run (not performance evidence), or
`--verify` for untimed corpus verification only. A recorded run requires a
clean committed source revision. Add `--artifacts .\BenchmarkDotNet.Artifacts\audio-run-1`
and repeat serially for run 2 and run 3. Do not run builds/tests concurrently.
The runner emits full JSON, summary CSV, measurement CSV, GitHub Markdown, and
`audio-corpus.json` with revision, runtime, SIMD widths, settings, hashes and
verified fallback frequencies.

This standalone net10.0 project pins BenchmarkDotNet 0.15.8 and
System.Numerics.Tensors 10.0.12. It source-links the actual production helper and
the independent original-order scalar oracle, frozen from
`273b0182745a3093c0e09f306ca8a1fff6ef3c5a`. It does not reference WinUI, Shared,
Whisper, ONNX, or a test assembly, and is not in the default build/CI path.
The standard out-of-process throughput job keeps adaptive iteration/warmup
selection and requests 2% relative error. BDN Error is half the **99.9%**
confidence interval. The user's power plan is preserved; no affinity changes
or ISA disablement are configured for recorded measurements.

## Frozen matrix and adoption gates

Meter: 480/512/960/1600/4096, silence/speech/clipped.
Boundary diagnostics: 16/31/32/33/511/513/4097/16000, speech.
VAD: 512, both 0.03f and 0.008f thresholds, quiet/speech/mixed/below/at/above.
Gain: 480/512/960/1600/4096, low/speech/clipped, scalar/tensor/copy-only.
Composed: fixed capture, ordinary speech/silence, clipping-heavy, and exactly
10%, 50%, 100% guard fallback. Every composed case compares scalar, meter-only,
meter+VAD, meter+gain, and all candidates.

Primary rows are meter speech at 480/960/1600, VAD quiet and speech at both
thresholds, and gain speech at 480/960/1600. Boundaries are not acceptance rows.
For adoption, at least one primary row must have mean ratio <=0.90 and candidate
upper confidence limit below scalar lower limit in **all three runs**.
Every composed row, including fixed and 100% fallback, must have
`(candidate mean + error)/(baseline mean - error) <=1.05` in all runs.
The denominator must be positive. An add-on must also pass against the retained
meter combination, not merely the original scalar stage. No new numeric
allocations or speech-decision mismatches are allowed. Unmet precision is
reported, not hidden. These gates must not be changed after measurement.

RMS inputs are immutable. Each gain/stage operation copies immutable input into
preallocated work buffers before processing. Copy cost is included symmetrically,
never subtracted. The gain benchmark returns the entire fully written reusable
buffer, keeping every processed sample externally observable without an extra
allocation or checksum pass. Stage checksums depend on every meter and VAD result/decision.
The stage has 20 callbacks (1600 samples for fixed capture, 512 otherwise),
three silent chunks to leave speaking state, and no allocations in the timed
operation. It excludes capture, resampling, lists, event subscribers, native
inference and OS work. It is a numerical-stage proxy, not a callback benchmark.

Corpus seed is xorshift32 `0xA0D1051A`. Speech combines a period-97 triangle
(amplitude 0.09) with noise (amplitude 0.03). Clipped data clamps a triangle
scaled by three. Quiet VAD is 0.2 times the active threshold, speech alternates
four times it, mixed data has 0.5 impulses every 13 samples with tiny background.
Threshold VAD uses adjacent floats and the exact float threshold.
Fallback stage chunks use the active threshold divided by five before gain,
verified after actual gain and reduction. Hashes are SHA256 of little-endian
IEEE float bytes. Setup rejects changed fallback rates or sample/decision
mismatches before timing.

## Numerical contract and derivation

Only post-gain/clamp finite samples in [-1,1] or NaN are in the RMS helper domain.
Meter vectorization is limited to lengths 32..4096; other lengths and
nonaccelerated runtimes retain ascending original-order float accumulation.
Empty input remains NaN. Nonfinite SIMD output is recomputed scalar, not
sanitized. RMS inputs are never mutated; gain is deliberately in-place.

For n samples, let u=2^-24, k=n+16, g=ku/(1-ku),
L=(1-u)^4 sqrt(1-g), U=(1+u)^4 sqrt(1+g), h=2^-55.
Under normal CLR round-to-nearest arithmetic, each RMS lies in
`[L*r-h, U*r+h]`, where r is the exact real RMS.

Inspection of runtime v10.0.12 `TensorPrimitives.Sum.cs` and
`Common/TensorPrimitives.IAggregationOperator.cs` shows separate squaring and
nonnegative addition, one accumulator per vector lane, and a horizontal sum.
Float inputs disable address-dependent alignment. Beginning/end masks eliminate
overlap; the full-block loop and trailing switch cover the remaining samples
once. The 128/256/512-bit paths have at most n/width+2 vector additions plus
the horizontal reduction; scalar depth is at most n+1 including squaring.
Both are below n+16. There is no cancellation, and no overflow for |x|<=1.
The four extra (1+/-u) factors cover division/square-root rounding with slack.

Underflow is not modeled by relative error. With eta=2^-126, allow loss eta
per elementary operation, including flushed tiny values. At most n+2*width
squares and n+6*width additions (including masked redundant lanes and
horizontal reduction) are below the loose budget 8*(n+16) for width<=16
and 32<=n<=4096. Propagating this allowance through the sum/division/sqrt
gives `sqrt(8*(n+16)*eta/(n*(1-ku)) + eta) <4e-19`, below h even after
rounding slack. This covers gradual underflow and flushed tiny contributions,
not arbitrary external rounding-mode changes.

The closed VAD band is `[(double)t*(1-2^-10), (double)t*(1+2^-10)]`.
These binary64 products of the exact promoted float thresholds are exact.
Inside/on the band, or for any unsupported shape/threshold/nonfinite result,
the original scalar result decides. Outside it, the ratios U/L and L/U imply
the same >= decision, with approximately 0.0009446 relative margin at n=512.
The actual pipeline comparisons/thresholds must remain unchanged.

An independent exact-rational check with 160-bit sqrt enclosures verifies all
n=32..4096 and both actual float thresholds under this arithmetic model.
At n=512, U-L+2h <=0.0000319490800744 <2^-14.
At n=4096 it is <=0.000245631224255 <**0.00025**.
The initial plan incorrectly compared this last value with 2^-12; the raw
meter tolerance was analytically corrected before any measurement.
The rounded meter bound includes both float multiplications:
`3*(U-L+2h)+6*u*(U+h) <=0.000737251344554 <2^-10`.
No tolerance applies to speech decisions, finite gain bits, or signed zeros.
NaN classification is preserved; undocumented NaN payload identity is not required.

The resolved package identifies dotnet/dotnet commit
`95017c711e6afc1085133d440e42b4bd78155701`; its net10.0 asset is used.
Available-host optimized JIT diagnostics show separate `vmulss`/`vaddss`
for the oracle, `vmulps`/`vaddps` for AVX-512 reduction, masked redundant lanes,
and `vdivss`/`vsqrtss` for RMS. The VAD comparisons use binary64 endpoints.
Diagnostics disabled tiering only to obtain optimized listings; normal tiering
and PGO remain enabled for recorded BDN runs.

This is conditional analysis, not empirical proof of all runtimes or of native
voice integration. Arbitrary external changes to floating-point control state
are outside the argument; live native voice proof remains unverified. Random
tests support the argument but cannot replace it. The guarded VAD above is now
only `AudioVadExperiment` in the benchmark project, source-linked into its
numerical tests. It is not compiled into the shipping app.

The selected gain method is the same two-pass in-place multiply/clamp experiment,
now owned by `AudioNumerics.ApplyGain` and source-linked into tests and benchmarks.
Finite output bits, signed zero, and exceptional-value classification match the
independent scalar oracle. The pipeline retains its original scalar VAD loop,
including accumulation order, both thresholds, comparisons, and state machine.

## Initial campaign and final selection

Measured source: `32eac4a9c5c6571d5ac6da6f2c1a0124b387cca4`.
Three complete serial Release invocations executed 145 cases each, with no
failures and zero reported managed allocation in all 435 rows.
Durations were 3:12:34, 2:46:44, and 3:13:12.
All summaries, full JSON and raw measurement CSV exports, corpus manifests,
per-row statistics/comparisons, gate results, and checksums are under
[`results/32eac4a9c5c6571d5ac6da6f2c1a0124b387cca4/x64`](results/32eac4a9c5c6571d5ac6da6f2c1a0124b387cca4/x64).
The full JSON and raw measurement CSV files are losslessly gzip-compressed.
Every archive was decompressed and compared byte-for-byte to its original;
no measurement or noisy/outlier row was removed. `archive-index.json` records
both stored and uncompressed SHA256 hashes.
The evidence directory disables Git text conversion for archived exports so
the committed bytes also match the hashes. The final evidence commit restores
the original CRLF Markdown-summary bytes that Git normalized in the first
archive commit; compressed raw measurements and statistical values were unchanged.
The evidence-only `results\evaluate-results.py` accepts either original or
compressed run directories. Reproduce the initial gates with:

```powershell
$evidence = ".\benchmarks\OpenClaw.Audio.Benchmarks\results\32eac4a9c5c6571d5ac6da6f2c1a0124b387cca4\x64"
python .\benchmarks\OpenClaw.Audio.Benchmarks\results\evaluate-results.py --source-sha 32eac4a9c5c6571d5ac6da6f2c1a0124b387cca4 --run "$evidence\run-1" --run "$evidence\run-2" --run "$evidence\run-3" --output .\BenchmarkDotNet.Artifacts\initial-gates
```

Use `--retained` for the final 93-case matrix. Evaluation rejects incomplete,
duplicate, dirty, smoke, mismatched-corpus, or mismatched-revision runs. It reports
precision misses independently instead of silently excluding them from the gates.

**Precision limitation: 128/145, 86/145, and 134/145 rows missed the requested
2% relative-error target (348/435 overall).** All measurements, including these
noisy rows and rejected experiments, are retained. The numbers below are not
application or transcription speedups. Confidence limits are BDN's 99.9% limits,
not 95% limits. A mean ratio and a conservative gate ratio are different metrics.

| Candidate | Primary mean improvement across all three runs | Worst relevant conservative composed ratio | Selection |
|---|---:|---:|---|
| Bounded meter RMS | 93.77% to 94.76% at speech 480/960/1600 | 0.800849 versus scalar | Retain for final-source measurement |
| Two-pass gain (includes reset copy) | 71.53% to 82.00% at speech 480/960/1600 | 0.875664 for meter+gain versus meter; 0.548164 versus scalar | Retain for final-source measurement |
| Guarded VAD | All four quiet/speech primary rows passed | 1.331495 for meter+VAD versus meter, 100% fallback | Reject; original scalar VAD restored |
| All candidates | Not an independent primary candidate | 1.133687 versus meter+gain, 100% fallback | Reject |

The rejected VAD addition's worst mean ratio versus meter was 1.212773
(21.28% slowdown), while its worst conservative ratio was 1.331495
(33.15% slowdown). In run 2 that same workload's mean ratio was only 1.0145,
but its conservative ratio 1.0558 still failed the 1.05 gate. Gains elsewhere
cannot offset this failure. The guard and corpus have not been narrowed or tuned.
The worst conservative meter+gain add-on row was run 3 ordinary audio:
mean ratio 0.797905, conservative ratio 0.875664.

After selection, the approved final matrix retains every meter/gain case and
all six composed workloads for scalar, meter, and meter+gain: 93 cases per run.
Already-rejected VAD methods and unchanged boundary diagnostics need not repeat.
The full 145-case original matrix remains available through `--filter "*"`.
Run the final matrix three times serially with distinct artifact directories:

```powershell
dotnet run -c Release --project .\benchmarks\OpenClaw.Audio.Benchmarks\OpenClaw.Audio.Benchmarks.csproj -- --filter "*AudioRmsBenchmarks*" "*AudioGainBenchmarks*" "*AudioStageBenchmarks.Scalar*" "*AudioStageBenchmarks.Meter" "*AudioStageBenchmarks.MeterGain*" --artifacts .\BenchmarkDotNet.Artifacts\audio-final-1
```

## Completed final-source campaign

Final measured source: `1fd070aa658d03668aeb69a715a040a5a28ee4f2`.
Its production code is the selection commit `0a2e7ca8d1bc4caaf7413ce541ca005821e5066a`;
the intervening change only archives initial evidence and documentation.
Three serial invocations completed all **279 cases (93 per run)** at this clean,
unchanged revision, with no failures and **zero reported allocated bytes in every
row**. Durations were 2:01:25, 1:19:56, and 1:25:15. Both retained candidates
passed the same primary rows in every run, with candidate upper 99.9% confidence
limits below baseline lower limits. All six composed workloads passed against
the original scalar baseline and the already-retained meter combination.

**Final precision limitation: 77/93, 45/93, and 45/93 rows missed the requested
2% relative-error target (167/279 overall).** The initial 348/435 misses remain
part of the evidence too. These are noisy measurements, not 2%-precision claims.
No case was omitted because it was slow or noisy; no guard, cutoff, corpus,
reset rule, confidence level, or acceptance gate was changed. Final corpus
hashes match the initial corpus exactly.

The following speech-profile times are mean ns/op. Each cell is
`scalar -> retained candidate`; gain includes the common reset copy.
Full errors, standard deviations, allocations, raw iterations, and every
diagnostic/composed row are in the exports rather than hidden by these summaries.

| Operation / samples | Run 1 | Run 2 | Run 3 |
|---|---:|---:|---:|
| Meter / 480 | 377.719 -> 23.867 | 417.035 -> 24.183 | 456.742 -> 31.994 |
| Meter / 960 | 1184.315 -> 53.113 | 873.121 -> 52.644 | 872.867 -> 52.517 |
| Meter / 1600 | 1941.132 -> 92.273 | 1460.078 -> 81.842 | 1492.926 -> 83.145 |
| Copy + gain / 480 | 517.026 -> 88.430 | 414.912 -> 73.801 | 416.036 -> 76.244 |
| Copy + gain / 960 | 692.634 -> 195.536 | 719.932 -> 154.612 | 706.526 -> 154.004 |
| Copy + gain / 1600 | 1175.905 -> 246.863 | 1192.591 -> 261.372 | 1192.492 -> 256.560 |

Meter primary mean improvements span **92.995% to 95.515%**; copy+gain spans
**71.769% to 82.896%**. These are arithmetic/fixture improvements only.
For the 20-callback numerical-stage proxy, the complete retained composition is:

| Workload | Scalar mean range (us/op) | Meter+gain mean range (us/op) | Mean improvement range | Worst conservative ratio vs scalar | Worst conservative ratio vs meter |
|---|---:|---:|---:|---:|---:|
| Fixed | 55.694-56.292 | 8.602-8.853 | 84.113%-84.719% | 0.165108 | 0.325915 |
| Ordinary | 28.243-29.626 | 12.901-15.539 | 47.549%-54.457% | 0.546253 | 0.748571 |
| Clipped | 27.257-29.339 | 12.126-12.822 | 55.512%-56.297% | 0.462935 | 0.685903 |
| 10% fallback corpus | 28.091-28.269 | 12.522-13.048 | 53.703%-55.459% | 0.481132 | 0.722240 |
| 50% fallback corpus | 27.227-28.380 | 12.684-13.215 | 53.412%-53.516% | 0.484828 | 0.711413 |
| 100% fallback corpus | 27.123-28.283 | 12.298-12.976 | 54.120%-54.657% | 0.475512 | 0.705697 |

"Fallback corpus" denotes the unchanged input's verified fallback frequency in
the rejected guarded experiment. Final production VAD is always the original
scalar loop, not a conditionally retained vector path.

There was no positive mean or conservative regression in any retained final
comparison. The worst meter-only composed ratio was 0.735136 mean / 0.776085
conservative. The worst final-composition ratio was 0.524506 mean / 0.546253
conservative versus scalar, and 0.713482 mean / 0.748571 conservative versus
meter. All three worst cases were run 1 ordinary audio; all are below the
unchanged 1.05 limit. The rejected VAD's 21.28% mean / 33.15% conservative
slowdown from the initial campaign is not erased by these retained-candidate wins.

Final durable exports and checksums:
[`results/1fd070aa658d03668aeb69a715a040a5a28ee4f2/x64`](results/1fd070aa658d03668aeb69a715a040a5a28ee4f2/x64).
The same lossless archive checks and privacy screening apply. Reproduce final gates:

```powershell
$evidence = ".\benchmarks\OpenClaw.Audio.Benchmarks\results\1fd070aa658d03668aeb69a715a040a5a28ee4f2\x64"
python .\benchmarks\OpenClaw.Audio.Benchmarks\results\evaluate-results.py --retained --source-sha 1fd070aa658d03668aeb69a715a040a5a28ee4f2 --run "$evidence\run-1" --run "$evidence\run-2" --run "$evidence\run-3" --output .\BenchmarkDotNet.Artifacts\final-gates
```

## Final validation and blockers

Original scalar pipeline characterization: 23 actual pipeline/lifecycle tests
passed, zero skipped, on native Windows x64. Pure helper plus existing speech
contracts and initial gain experiment: 90 passed, zero skipped, both normally and in a
separate process with `DOTNET_EnableHWIntrinsic=0`. The same 23 actual-pipeline
tests also passed against the initial candidate assemblies with matching SHA256
between production and test outputs. Those initial results preceded selection.
Host: Windows build 26200, Intel i9-11950H, .NET SDK 10.0.401, runtime 10.0.12.
Native ARM64, live microphone/UI, gateway and application-level performance
are not verified.

After restoring scalar VAD and promoting gain, the 90 pure/contract tests pass
both normally and ISA-disabled. The real pipeline suite now has 29 passing
cases, including large PCM16/float fixed-capture callbacks and exact scalar
VAD probability bits. The initial Shared/tray failures were environment-related:
the Shared test needs a direct-user ChangePermissions ACL on its temporary
directory, and interrupted WinUI builds had not copied app-local VC++ DLLs.
An authorized, newly created task-owned TEMP with only the current user's
inheritable FullControl added resolves the exact Shared failure. The existing
`CopyOpenClawVCRuntimeToOutput` target resolves all four native-runtime tests.
No shared/root-directory ACL or machine-wide setting was changed.
The subsequent complete mandatory-suite rerun passed Shared (4107 passed,
32 skipped, 4139 total) and Tray (3157 passed, zero skipped). The full build
still failed only at the unrelated WinUI npm dependency restore.
These final-code checks ran before the clean final measurement freeze. There
were no subsequent production, oracle, test, or benchmark changes, only
documentation and evidence. Sanitized commands, TRX-derived results and hashes,
and source fingerprints are in
[`results/source-validation.json`](results/source-validation.json).

| Requirement | Evidence |
|---|---|
| "EXACT identical speech decisions" | `HysteresisAndSegmentation_MatchScalarTraceAtExactConsumedChunks`: exact events, chunk boundaries, eligibility and scalar probability bits in 5 cases; original VAD source restored unchanged |
| "Gain/clamp finite output bits/signed zeros must be exact, with original NaN semantics." | `Gain_InPlaceSlices_PreserveEveryBitAndExceptionalClassification`: 25 shapes with offset slices and untouched sentinels; `NaNInput_EmitsNaNMeterAndDoesNotStartSpeech` |
| "Characterization and actual pipeline fake-capture tests precede adoption." | Original 23-case trace passed before substitution; final `FixedCapture_PreservesPostGainSampleBitsAndMeter_ButBypassesVad` covers 8 PCM16/float cases; final pipeline/lifecycle total 29 passed |
| "bounded RMS/meter/probability rounding" | `ShapesSignalsAndOffsetSlices_PreserveBoundsFallbackAndInput`, 25 shapes; `EmptyRms_RemainsNaN_NotSilentZero`; `verify-numerical-bounds.py`; final VAD probabilities are exact |
| "zero added steady-state numeric allocations" | MemoryDiagnoser reports 0 B/op for all 279 final records, including every composed workload |
| Required full Shared and Tray tests | 4107 Shared passed, 32 skipped; 3157 Tray passed, 0 skipped; final-code TRX counters retained |

Direct production, oracle, benchmark and evaluator review found no blocking
issue. The structured review attempt
`python .\.agents\skills\autoreview\scripts\autoreview --mode commit --commit 0a2e7ca8d1bc4caaf7413ce541ca005821e5066a`
could not run its Codex engine because the executable was unavailable. This is
not a clean autoreview claim. Initial optimized x64 JIT diagnostic output is
preserved in `results\initial-jit-x64.txt`; it is not final-run timing evidence.

Baseline recovery uses only process-scoped settings: short PATH (the inherited
10,141-character PATH breaks CMD's 8191-character limit), an explicit official
NuGet v2 configuration (v3 TLS fails), isolated TEMP/TMP/settings, and
OpenClawVsInstallRoot pointing to the actual VS2022 install containing VC++
14.44.35211. No global settings, certificates, or account changes were made.

The mandatory full build is currently blocked by npm TLS failures downloading
locked MXC dependencies. The public Microsoft npm mirror returns 401 for
mxc-sdk 0.8.0 and node-pty 1.2.0-beta.12; offline restore reports ENOTCACHED.
Focused pipeline tests used compiled real assemblies and the repository's
asset-copy target, then `--no-build`; this is not a passing full build.
The exact failing download is
`https://registry.npmjs.org/node-pty/-/node-pty-1.2.0-beta.12.tgz`
with `ERR_SSL_SSL/TLS_ALERT_HANDSHAKE_FAILURE`. Independent official-endpoint
checks also failed. No TLS bypass, account switch, changed lockfile, substitute
binary, or privileged installation was used.

To clear this remaining gate, restore authorized working HTTPS access to the
locked npm dependencies, run `npm ci --no-audit --no-fund` in the isolated
worktree, then rerun `.\build.ps1`, full Shared/Tray tests and focused pipeline
tests with the same task-local settings/TEMP isolation. Do not treat the
successful scoped compile/copy/test path as the required complete app build.

The original publication policy was revised by user direction to permit a
transparent discussion proposal while this unrelated full-build blocker remains.
The fork is
[`perf/tensorprimitives-audio`](https://github.com/luisquintanilla/openclaw-windows-node/tree/perf/tensorprimitives-audio);
the coordinating session published
[openclaw/openclaw-windows-node#1524](https://github.com/openclaw/openclaw-windows-node/issues/1524).
Evidence commits preserve the immutable initial and final measured revisions.
No PR, ownership labels, merge-readiness, native ARM64, live microphone/UI/gateway,
or application/transcription speedup claim is made.
