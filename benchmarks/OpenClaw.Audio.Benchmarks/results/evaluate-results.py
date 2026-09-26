"""Evaluate the frozen 145-case audio experiment without changing its gates."""

import argparse
import csv
import gzip
import itertools
import json
import math
import re
from pathlib import Path


METER = "AudioRmsBenchmarks"
BOUNDARY = "AudioRmsBoundaryBenchmarks"
VAD = "AudioVadBenchmarks"
GAIN = "AudioGainBenchmarks"
STAGE = "AudioStageBenchmarks"
SCENARIOS = ("Fixed", "Ordinary", "Clipped", "Fallback10", "Fallback50", "Fallback100")
STAGE_PAIRS = (
    ("Scalar", "Meter"), ("Scalar", "MeterVad"), ("Scalar", "MeterGain"),
    ("Scalar", "All"), ("Meter", "MeterVad"), ("Meter", "MeterGain"),
    ("Meter", "All"), ("MeterVad", "All"), ("MeterGain", "All"),
)


def key(group, method, params):
    return group, method, tuple(sorted(params.items()))


def parameters(text):
    pairs = [part.strip().split("=", 1) for part in re.split(r"[,;&]", text)]
    result = {}
    for name, value in pairs:
        name, value = name.strip(), value.strip().strip("'\"")
        if name in result:
            raise ValueError(f"Duplicate parameter: {text}")
        result[name] = int(value) if name == "Length" else float(value) if name == "Threshold" else value
    return result


def expected_keys(retained=False):
    result = set()
    for length, profile, method in itertools.product(
        (480, 512, 960, 1600, 4096), ("Silence", "Speech", "Clipped"), ("Scalar", "Meter")
    ):
        result.add(key(METER, method, {"Length": length, "Profile": profile}))
    for length, method in itertools.product((16, 31, 32, 33, 511, 513, 4097, 16000), ("Scalar", "Meter")):
        result.add(key(BOUNDARY, method, {"Length": length}))
    for threshold, profile, method in itertools.product(
        (0.03, 0.008), ("Quiet", "Speech", "Mixed", "Below", "At", "Above"), ("Scalar", "Guarded")
    ):
        result.add(key(VAD, method, {"Threshold": threshold, "Profile": profile}))
    for length, profile, method in itertools.product(
        (480, 512, 960, 1600, 4096), ("Low", "Speech", "Clipped"), ("Scalar", "Tensor", "CopyOnly")
    ):
        result.add(key(GAIN, method, {"Length": length, "Profile": profile}))
    for scenario, method in itertools.product(SCENARIOS, ("Scalar", "Meter", "MeterVad", "MeterGain", "All")):
        result.add(key(STAGE, method, {"Scenario": scenario}))
    assert len(result) == 145
    if retained:
        result = {row for row in result if row[0] in (METER, GAIN) or
                  row[0] == STAGE and row[1] in ("Scalar", "Meter", "MeterGain")}
        assert len(result) == 93
    return result


def finite(value, name):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f"Missing/nonfinite {name}: {value!r}")
    return value


def load_run(root, run, revision, retained=False):
    manifest = json.loads((root / "audio-corpus.json").read_text(encoding="utf-8-sig"))
    required = {
        "Revision": revision, "Dirty": False, "Smoke": False,
        "Tensors": "10.0.12", "BenchmarkDotNet": "0.15.8",
        "RelativeErrorTarget": 0.02, "ErrorConfidence": "99.9%",
    }
    for field, expected in required.items():
        if manifest.get(field) != expected:
            raise ValueError(f"Run {run}: {field} does not match {expected!r}")
    if any(value is not None for value in manifest["RuntimeOverrides"].values()):
        raise ValueError(f"Run {run}: nondefault recorded runtime overrides")
    if len(manifest["Corpus"]) != 46:
        raise ValueError(f"Run {run}: expected 46 verified corpus records")
    fallback = {entry["Profile"]: (entry["Fallbacks"], entry["Chunks"])
                for entry in manifest["Corpus"] if entry["Group"] == "Stage"}
    if fallback != {"Fixed": (0, 0), "Ordinary": (0, 20), "Clipped": (0, 20),
                    "Fallback10": (2, 20), "Fallback50": (10, 20), "Fallback100": (20, 20)}:
        raise ValueError(f"Run {run}: fallback matrix changed")
    rows = {}
    environment = None
    reports = list((root / "results").glob("*-report-full.json*"))
    for file in sorted(reports):
        payload = gzip.decompress(file.read_bytes()) if file.suffix == ".gz" else file.read_bytes()
        report = json.loads(payload.decode("utf-8-sig"))
        host = report["HostEnvironmentInfo"]
        if environment is not None and host != environment:
            raise ValueError(f"Run {run}: inconsistent host metadata")
        environment = host
        if (host["BenchmarkDotNetVersion"] != "0.15.8" or
                host["Configuration"] != "RELEASE" or host["HasAttachedDebugger"]):
            raise ValueError(f"Run {run}: unexpected BDN environment")
        for bench in report["Benchmarks"]:
            params = parameters(bench["Parameters"])
            row_key = key(bench["Type"], bench["Method"], params)
            if row_key in rows:
                raise ValueError(f"Run {run}: duplicate {row_key}")
            if "AudioDefault(" not in bench["DisplayInfo"] or "SmokeNotEvidence" in bench["DisplayInfo"]:
                raise ValueError(f"Run {run}: unexpected job")
            stats = bench["Statistics"]
            ci = stats["ConfidenceInterval"]
            if ci["Level"] != 12 or stats["N"] < 2:
                raise ValueError(f"Run {run}: missing 99.9% interval")
            mean = finite(stats["Mean"], "mean")
            error = finite(ci["Margin"], "error")
            deviation = finite(stats["StandardDeviation"], "standard deviation")
            allocated = finite(bench["Memory"]["BytesAllocatedPerOperation"], "allocation")
            if mean <= 0 or min(error, deviation, allocated) < 0:
                raise ValueError(f"Run {run}: invalid statistics")
            if not any(m["IterationMode"] == "Workload" and m["IterationStage"] == "Result"
                       for m in bench["Measurements"]):
                raise ValueError(f"Run {run}: missing raw result measurements")
            rows[row_key] = {
                "Run": run, "Group": bench["Type"], "Method": bench["Method"],
                "Parameters": json.dumps(params, sort_keys=True, separators=(",", ":")),
                "MeanNs": mean, "ErrorNs99_9": error, "StandardDeviationNs": deviation,
                "N": stats["N"], "AllocatedBytes": allocated,
                "RelativeError": error / mean, "PrecisionTargetMet": error / mean <= 0.02,
            }
    expected = expected_keys(retained)
    if set(rows) != expected:
        raise ValueError(f"Run {run}: missing={expected - set(rows)}, extra={set(rows) - expected}")
    return manifest, environment, rows


def compare(baseline, candidate):
    lower = baseline["MeanNs"] - baseline["ErrorNs99_9"]
    upper = candidate["MeanNs"] + candidate["ErrorNs99_9"]
    conservative = upper / lower if lower > 0 else None
    ratio = candidate["MeanNs"] / baseline["MeanNs"]
    return {
        "Run": candidate["Run"], "Group": candidate["Group"], "Parameters": candidate["Parameters"],
        "Baseline": baseline["Method"], "Candidate": candidate["Method"],
        "BaselineMeanNs": baseline["MeanNs"], "BaselineErrorNs99_9": baseline["ErrorNs99_9"],
        "BaselineStdDevNs": baseline["StandardDeviationNs"], "BaselineBytes": baseline["AllocatedBytes"],
        "CandidateMeanNs": candidate["MeanNs"], "CandidateErrorNs99_9": candidate["ErrorNs99_9"],
        "CandidateStdDevNs": candidate["StandardDeviationNs"], "CandidateBytes": candidate["AllocatedBytes"],
        "MeanRatio": ratio, "MeanImprovementPercent": (1 - ratio) * 100,
        "ConservativeRatio": conservative,
        "BeyondNoise": lower > 0 and upper < lower,
        "PrimaryWin": ratio <= 0.90 and lower > 0 and upper < lower,
        "ComposedGate": lower > 0 and conservative <= 1.05,
    }


def comparisons(rows, retained=False):
    result = []
    for row_key, row in rows.items():
        group, method, params = row_key
        if group == STAGE or method == "Scalar":
            continue
        result.append(compare(rows[(group, "Scalar", params)], row))
    pairs = (("Scalar", "Meter"), ("Scalar", "MeterGain"), ("Meter", "MeterGain")) if retained else STAGE_PAIRS
    for scenario, (baseline, candidate) in itertools.product(SCENARIOS, pairs):
        params = {"Scenario": scenario}
        result.append(compare(rows[key(STAGE, baseline, params)], rows[key(STAGE, candidate, params)]))
    return result


def write_csv(path, rows):
    with path.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def evaluate(runs, revision, retained=False):
    all_rows, all_comparisons = [], []
    first_manifest = first_environment = None
    for number, root in enumerate(runs, 1):
        manifest, environment, rows = load_run(root, number, revision, retained)
        if number > 1 and (manifest != first_manifest or environment != first_environment):
            raise ValueError(f"Run {number}: corpus/runtime/host differs between independent runs")
        first_manifest, first_environment = manifest, environment
        all_rows.extend(rows.values())
        all_comparisons.extend(comparisons(rows, retained))

    primary = {}
    for name, group, method, param_list in (
        ("Meter", METER, "Meter", [{"Length": n, "Profile": "Speech"} for n in (480, 960, 1600)]),
        ("Vad", VAD, "Guarded", [{"Threshold": t, "Profile": p}
                               for t, p in itertools.product((0.03, 0.008), ("Quiet", "Speech"))]),
        ("Gain", GAIN, "Tensor", [{"Length": n, "Profile": "Speech"} for n in (480, 960, 1600)]),
    ):
        if retained and name == "Vad":
            continue
        repeated = []
        for params in param_list:
            matches = [r for r in all_comparisons if r["Group"] == group and r["Candidate"] == method
                       and json.loads(r["Parameters"]) == params]
            if len(matches) != 3:
                raise ValueError(f"Primary row does not have all three runs: {params}")
            if all(r["PrimaryWin"] for r in matches):
                repeated.append({"Parameters": params, "Comparisons": matches})
        primary[name] = {"Passed": bool(repeated), "RepeatableWinningRows": repeated}

    stage_gates = {}
    pairs = (("Scalar", "Meter"), ("Scalar", "MeterGain"), ("Meter", "MeterGain")) if retained else STAGE_PAIRS
    for baseline, candidate in pairs:
        matches = [r for r in all_comparisons if r["Group"] == STAGE and
                   r["Baseline"] == baseline and r["Candidate"] == candidate]
        if len(matches) != 18:
            raise ValueError("Missing a composed acceptance row")
        worst = max(matches, key=lambda r: r["ConservativeRatio"]
                    if r["ConservativeRatio"] is not None else math.inf)
        stage_gates[f"{candidate}/{baseline}"] = {
            "Passed": all(r["ComposedGate"] for r in matches),
            "WorstConservative": worst,
            "WorstMean": max(matches, key=lambda r: r["MeanRatio"]),
            "Failures": [r for r in matches if not r["ComposedGate"]],
        }

    result = {
        "MeasuredRevision": revision, "CompleteRuns": 3, "CasesPerRun": 93 if retained else 145,
        "Matrix": "Retained meter/gain" if retained else "Initial complete experiment",
        "ConfidenceLevel": "99.9%", "RelativeErrorTarget": 0.02,
        "Host": first_environment,
        "PrimaryGates": primary, "ComposedGates": stage_gates,
        "AllocationViolations": [r for r in all_rows if r["AllocatedBytes"] != 0],
        "PrecisionMisses": [r for r in all_rows if not r["PrecisionTargetMet"]],
        "Interpretation": (
            "Statistical gates only. Correctness, final measured source, mandatory build/tests, "
            "and publication authorization remain separate requirements. A primary win must be "
            "the same row in all three runs. Add-ons must pass against the already-retained combination."
        ),
    }
    return result, all_rows, all_comparisons


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--run", type=Path, action="append", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--retained", action="store_true", help="Require all 93 retained meter/gain acceptance cases")
    args = parser.parse_args()
    if len(args.run) != 3 or len({p.resolve() for p in args.run}) != 3:
        parser.error("Exactly three distinct complete run directories are required")
    if not re.fullmatch("[0-9a-f]{40}", args.source_sha):
        parser.error("Use the full immutable measured source SHA")
    result, rows, contrasts = evaluate(args.run, args.source_sha, args.retained)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "gate-results.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    write_csv(args.output / "all-statistics.csv", rows)
    write_csv(args.output / "all-comparisons.csv", contrasts)
    print(json.dumps({
        "Source": args.source_sha, "Runs": result["CompleteRuns"], "Records": len(rows),
        "Primary": {name: gate["Passed"] for name, gate in result["PrimaryGates"].items()},
        "Composed": {name: gate["Passed"] for name, gate in result["ComposedGates"].items()},
        "AllocationViolations": len(result["AllocationViolations"]),
        "PrecisionMisses": len(result["PrecisionMisses"]),
    }, indent=2))


if __name__ == "__main__":
    main()
