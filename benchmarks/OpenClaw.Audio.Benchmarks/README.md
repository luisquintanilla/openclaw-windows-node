# Managed audio arithmetic experiment

Status: meter plus gain selected from the initial three-run experiment.
Original scalar VAD is restored. Final-source measurement and mandatory full-build
validation are still pending, so this is not a qualified or published optimization.
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

Only post-gain/clamp finite samples in [-1,1] or NaN are in the helper domain.
Meter vectorization is limited to lengths 32..4096; other lengths and
nonaccelerated runtimes retain ascending original-order float accumulation.
Empty input remains NaN. Nonfinite SIMD output is recomputed scalar, not
sanitized. Inputs are never mutated.

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

Final selection requires the actual source-linked meter/gain implementation to
pass the same primary and composed gates on its new measured revision. No
cutoffs, thresholds, corpus, reset rules, error targets, or acceptance gates change.

## Current validation and blockers

Original scalar pipeline characterization: 23 actual pipeline/lifecycle tests
passed, zero skipped, on native Windows x64. Pure helper plus existing speech
contracts and gain experiment: 90 passed, zero skipped, both normally and in a
separate process with `DOTNET_EnableHWIntrinsic=0`. The same 23 actual-pipeline
tests also pass against current candidate assemblies with matching SHA256
between production and test outputs. These are not final closeout results.
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
No branch push or upstream proposal is permitted while that mandatory gate
remains blocked. Measurement/selection results will be added only after runs.
