"""Archive complete BDN exports losslessly, with deterministic gzip and checksums."""

import argparse
import gzip
import hashlib
import json
import re
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--run", type=Path, action="append", required=True)
    parser.add_argument("--analysis", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if len(args.run) != 3 or not re.fullmatch("[0-9a-f]{40}", args.source_sha):
        parser.error("Require three runs and a full measured source SHA")
    if args.output.exists():
        raise ValueError("Refusing to overwrite an existing evidence archive")
    sources = []
    for number, root in enumerate(args.run, 1):
        manifest = json.loads((root / "audio-corpus.json").read_text(encoding="utf-8-sig"))
        if manifest["Revision"] != args.source_sha or manifest["Dirty"] or manifest["Smoke"]:
            raise ValueError("Archive must contain clean recorded measurements at the specified source")
        sources.append((root / "audio-corpus.json", Path(f"run-{number}") / "audio-corpus.json"))
        for file in sorted((root / "results").iterdir()):
            if file.name.endswith(("-report-full.json", "-measurements.csv", "-report.csv", "-report-github.md")):
                sources.append((file, Path(f"run-{number}") / "results" / file.name))
    for file in sorted(args.analysis.iterdir()):
        if file.name in ("gate-results.json", "all-statistics.csv", "all-comparisons.csv"):
            sources.append((file, Path("analysis") / file.name))
    checked = []
    for source, relative in sources:
        content = source.read_bytes()
        text = content.decode("utf-8-sig")
        if re.search(r"(?i)([A-Z]:[\\/]|\\\\[^\\\s]+\\|authorization\s*:|bearer\s+|api[_-]?key\s*[:=])", text):
            raise ValueError(f"Review potentially identifying/sensitive content before archiving: {source.name}")
        compress = source.name.endswith(("-report-full.json", "-measurements.csv"))
        output = gzip.compress(content, compresslevel=9, mtime=0) if compress else content
        if compress and gzip.decompress(output) != content:
            raise ValueError("Compression round-trip failed")
        if compress:
            relative = relative.with_suffix(relative.suffix + ".gz")
        checked.append((relative, content, output))
    args.output.mkdir(parents=True)
    entries = []
    for relative, content, output in checked:
        destination = args.output / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(output)
        if destination.read_bytes() != output:
            raise ValueError("Archive read-back verification failed")
        entries.append({
            "Path": relative.as_posix(), "StoredBytes": len(output), "UncompressedBytes": len(content),
            "StoredSha256": hashlib.sha256(output).hexdigest(),
            "UncompressedSha256": hashlib.sha256(content).hexdigest(),
        })
    (args.output / "archive-index.json").write_text(json.dumps({
        "MeasuredRevision": args.source_sha,
        "Description": "All three runs. Full JSON/raw measurement CSV are losslessly gzip-compressed. "
                       "No measurement or noisy/outlier row was removed. Standard summaries are uncompressed.",
        "Files": entries,
    }, indent=2) + "\n", encoding="utf-8")
    print(f"Archived {len(entries)} files, {sum(e['StoredBytes'] for e in entries):,} stored bytes; all round-trips verified.")


if __name__ == "__main__":
    main()
