#!/usr/bin/env python3
"""Markdown counts table for one host's results.txt: runs and losses per variant and mode, split by load bucket
(the mean of the 1-minute load average before and after each run). A run is lost when the field's final text (and,
in EditableLab, the model's value) isn't what was typed; variant D expects upper case.

Usage: tabulate.py <results.txt> [--text ...] [--only-tags prefix,...] [--skip-tags prefix,...]
"""
import argparse
import re
from collections import defaultdict

LINE = re.compile(r"tag=(\S+) round=\d+ variant=(\S+) mode=(\S+) load0=(\S+) load1=(\S+) seconds=\S+ "
                  r"field=(.*)\|model=(.*)$")
BUCKETS = ["<20", "20-40", "40-80", ">=80"]


def bucket(load):
    return "<20" if load < 20 else "20-40" if load < 40 else "40-80" if load < 80 else ">=80"


def main():
    p = argparse.ArgumentParser()
    p.add_argument("results")
    p.add_argument("--text", default="The quick brown fox jumps over")
    p.add_argument("--only-tags", default="")
    p.add_argument("--skip-tags", default="")
    a = p.parse_args()
    only = [t for t in a.only_tags.split(",") if t]
    skip = [t for t in a.skip_tags.split(",") if t]
    cells = defaultdict(lambda: defaultdict(lambda: [0, 0]))
    for raw in open(a.results, encoding="utf-8"):
        m = LINE.search(raw.rstrip("\n"))
        if not m:
            continue
        tag, variant, mode, l0, l1, field, model = m.groups()
        if only and not any(tag.startswith(t) for t in only):
            continue
        if any(tag.startswith(t) for t in skip):
            continue
        expected = a.text.upper() if variant == "D" else a.text
        lost = field.replace("\\n", "\n") != expected or (model != "?" and model != expected)
        b = bucket((float(l0) + float(l1)) / 2)
        for key in ((variant, mode), (variant, "all")):
            cells[key][b][0] += 1
            cells[key][b][1] += lost
    print("| Variant | Mode | Runs | Lost | " + " | ".join(f"load {b} (runs/lost)" for b in BUCKETS) + " |")
    print("|---|---|---|---|" + "---|" * len(BUCKETS))
    for (variant, mode) in sorted(cells):
        c = cells[(variant, mode)]
        runs = sum(v[0] for v in c.values())
        lost = sum(v[1] for v in c.values())
        parts = [f"{c[b][0]}/{c[b][1]}" if b in c else "-" for b in BUCKETS]
        print(f"| {variant} | {mode} | {runs} | {lost} | " + " | ".join(parts) + " |")


if __name__ == "__main__":
    main()
