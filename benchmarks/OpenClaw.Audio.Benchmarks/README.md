# Managed audio arithmetic experiment

Status: local RMS candidates integrated for evaluation, not a qualified optimization.
Production candidate selection is pending correctness and performance gates.
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
tests support the argument but cannot replace it. Gain is a separate two-pass
benchmark-only experiment.

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
