#!/usr/bin/env python3
"""Counts the Bitwarden-host runs of the issue 977 harness and locates each lost character.

Usage: analyze-bitwarden.py <results.txt> <logs dir> [--text ...] [--details]

Per run it reads <run>.sw.jsonl (Swift: ui, uiChange, model, key, swState) and <run>.cs.jsonl (C#: csSet, csGet), both
timed on the same uptime clock. The final field text is the XCUITest accessibility value; the final model is the last
"model" sample (generated model's settled notes), or the last swState for the plain @State variant. For the first
character that differs it reports never-in-field, field-not-delivered (UIKit had it, C#/the @State never did) or
delivered-then-lost (it reached C# or the @State, later text didn't have it).
"""
import argparse
import json
import os
import re
from collections import defaultdict

LINE = re.compile(r"run=(\S+) tag=(\S+) round=(\d+) variant=(\S+) mode=(\S+) load0=(\S+) load1=(\S+) seconds=(\S+) "
                  r"field=(.*)\|model=(.*)$")


def bucket(load):
    for limit in (20, 40, 80):
        if load < limit:
            return f"<{limit}"
    return ">=80"


def read(path):
    if not os.path.exists(path):
        return []
    return [json.loads(x) for x in open(path, encoding="utf-8-sig") if x.strip()]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("results")
    parser.add_argument("logs")
    parser.add_argument("--text", default="The quick brown fox jumps over")
    parser.add_argument("--details", action="store_true")
    args = parser.parse_args()
    typed = args.text
    table = defaultdict(lambda: [0, 0])
    where = defaultdict(int)
    rows = []
    for raw in open(args.results, encoding="utf-8"):
        m = LINE.search(raw.rstrip("\n"))
        if not m:
            if "status=" in raw:
                rows.append((raw.strip(),))
            continue
        run, tag, _round, variant, mode, l0, l1, seconds, field, _ = m.groups()
        load = (float(l0) + float(l1)) / 2
        sw = read(os.path.join(args.logs, f"{run}.sw.jsonl"))
        cs = read(os.path.join(args.logs, f"{run}.cs.jsonl"))
        entries = sorted(sw + cs, key=lambda e: e["t"])
        delivered_kind = "swState" if variant == "state" else "csSet"
        delivered = [e for e in entries if e["kind"] == delivered_kind]
        models = [e for e in entries if e["kind"] == ("swState" if variant == "state" else "model")]
        model = models[-1]["text"] if models else ""
        field = field.replace("\\n", "\n")
        lost = field != typed or model != typed
        key = (variant, mode, bucket(load))
        table[key][0] += 1
        note = ""
        if lost:
            table[key][1] += 1
            actual = model if model != typed else field
            i = next((k for k, (a, b) in enumerate(zip(typed, actual)) if a != b), min(len(typed), len(actual)))
            prefix = typed[: i + 1]
            in_field = any(e["kind"] in ("ui", "uiChange") and e["text"].startswith(prefix) for e in entries)
            in_delivered = any(e["text"].startswith(prefix) for e in delivered)
            kind = ("delivered-then-lost" if in_delivered else "field-not-delivered" if in_field
                    else "never-in-field")
            where[(variant, mode, kind)] += 1
            note = f"lost index {i} ({typed[i] if i < len(typed) else 'end'!r}) -> {kind}; field={field!r} model={model!r}"
        csget_off_main = sum(1 for e in cs if not e.get("main", True))
        rows.append((run, variant, mode, f"{load:.1f}", seconds, len(delivered), f"offmain={csget_off_main}",
                     "LOST" if lost else "ok", note))
    print("variant mode load-bucket runs lost")
    for key in sorted(table):
        print(*key, *table[key])
    print("\nlosses by layer")
    for key in sorted(where):
        print(*key, where[key])
    if args.details:
        print("\nruns")
        for row in rows:
            print(*row)


if __name__ == "__main__":
    main()
