#!/usr/bin/env python3
"""Counts issue 977 harness runs and locates each lost character.

Usage: analyze.py <results.txt> <logs dir> [--text "The quick brown fox jumps over"] [--details]

A run is lost when the model's final value, or the field's final UIKit text, isn't what was typed (variant D: the
model must be the upper-cased text). For the first character that differs, it says where the character got to:
  never-in-field   no UIKit text ever had it (the keystroke never reached the field: harness or input system)
  field-not-setter UIKit had it, the binding setter never got it
  setter-then-lost the setter got it, and later text did not have it (overwritten after it was delivered)
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


def first_diff(expected, actual):
    for i, (a, b) in enumerate(zip(expected, actual)):
        if a != b:
            return i
    return min(len(expected), len(actual))


def locate(entries, typed, index, fold):
    prefix = typed[: index + 1]
    norm = (lambda s: s.lower()) if fold else (lambda s: s)
    prefix = norm(prefix)
    in_field = [e for e in entries if e["kind"] in ("ui", "uiChange") and norm(e["text"]).startswith(prefix)]
    in_setter = [e for e in entries if e["kind"] == "edit" and norm(e["text"]).startswith(prefix)]
    if not in_field and not in_setter:
        return "never-in-field", None
    if not in_setter:
        return "field-not-setter", in_field[0]["seq"]
    return "setter-then-lost", in_setter[0]["seq"]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("results")
    parser.add_argument("logs")
    parser.add_argument("--text", default="The quick brown fox jumps over")
    parser.add_argument("--details", action="store_true")
    args = parser.parse_args()
    typed = args.text
    table = defaultdict(lambda: [0, 0, 0])  # runs, lost, holds-started
    where = defaultdict(int)
    rows = []
    for raw in open(args.results, encoding="utf-8"):
        m = LINE.search(raw.rstrip("\n"))
        if not m:
            continue
        run, tag, _round, variant, mode, l0, l1, seconds, field, model = m.groups()
        load = (float(l0) + float(l1)) / 2
        expected_model = typed.upper() if variant == "D" else typed
        log_path = os.path.join(args.logs, f"{run}.jsonl")
        entries = []
        if os.path.exists(log_path):
            entries = [json.loads(x) for x in open(log_path, encoding="utf-8") if x.strip()]
        edits = [e for e in entries if e["kind"] == "edit"]
        answers = [e for e in entries if e["kind"] == "answer"]
        holds = sum(1 for e, a in zip(edits, answers) if e["text"] != a["text"]) if variant in "BCDSL" else 0
        lost = model != expected_model or field != expected_model
        key = (variant, mode, bucket(load), "synthetic" if tag.startswith("burn") else "ambient")
        table[key][0] += 1
        table[key][2] += holds
        note = ""
        if lost:
            table[key][1] += 1
            actual = model if model != expected_model else field
            i = first_diff(expected_model, actual)
            kind, seq = locate(entries, typed, i, fold=variant == "D")
            where[(variant, mode, kind)] += 1
            note = f"lost index {i} ({typed[i]!r}) -> {kind} (seq {seq}); field={field!r} model={model!r}"
        rows.append((run, variant, mode, f"{load:.1f}", seconds, len(edits), holds, "LOST" if lost else "ok", note))
    print("variant mode load-bucket load-kind runs lost hold-starts")
    for key in sorted(table):
        runs, lost, holds = table[key]
        print(*key, runs, lost, holds)
    print("\nlosses by layer")
    for key in sorted(where):
        print(*key, where[key])
    if args.details:
        print("\nruns")
        for row in rows:
            print(*row)


if __name__ == "__main__":
    main()
