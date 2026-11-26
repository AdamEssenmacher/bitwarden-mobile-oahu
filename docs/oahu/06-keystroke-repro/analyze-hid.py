#!/usr/bin/env python3
"""Round 2 (hardware-keyboard path) analyzer for the issue 977 Bitwarden runs written by run-hid.sh.

Usage: analyze-hid.py <out dir> [--details]      every run in <out dir>/results.txt
       analyze-hid.py --one <run> <out dir>       one run, printed after it is collected

Per run it reads <run>.sw.jsonl (Swift: hidDown/hidUp from GameController, ui/uiChange/uiUnfocused, model, swState,
kbShow/kbHide, hwKeyboard) and <run>.cs.jsonl (C#: csSet, csGet), on one uptime clock.

- typed: the text the control tool was asked to type (TEXTS below, by text id).
- hid: the key sequence the app received as HID key-downs, decoded from HID usage codes (letters, space, Return).
- field: the field's last UIKit text (ui, uiChange or uiUnfocused, whichever is last).
- model: the generated model's last settled notes (the plain @State's last value for variant `state`).

A hardware Return ends editing of the vertical notes field (step 5, G34), so the text after the first Return does not
reach it. The run is scored on `expect`: typed up to and including the first Return for texts with Return, else all
of it. A run is "lost" when the field or the model differs from `expect`, and "cut" when the field is a strict prefix
of `expect` (keys stopped arriving; not a dropped character inside the text). For the first differing character the
layer is: not-in-hid (the app never received the key), hid-not-in-field (received, never in the UIKit text),
field-not-delivered (in the field, never in a setter value), delivered-then-lost (delivered, later overwritten).
"""
import argparse
import json
import os
import re
from collections import defaultdict

TEXTS = {
    "fox": "The quick brown fox jumps over",
    "line5": "Line one\nLine two\nLine three\nLine four\nLine five teh",
    "line3": "Line one\nLine two\nLine three",
    "alpha": "Alpha beta gamma delta epsilon",
}
LINE = re.compile(r"run=(\S+) tag=(\S+) round=\d+ variant=(\S+) mode=(\S+) text=(\S+) load0=(\S+) load1=(\S+) "
                  r"seconds=(\S+) yes=(\d+)")
SHIFT = {225, 229}


def decode(code, shift):
    if 4 <= code <= 29:
        c = chr(ord("a") + code - 4)
        return c.upper() if shift else c
    return {44: " ", 40: "\n", 42: "<bs>"}.get(code, f"<{code}>")


def read(path):
    if not os.path.exists(path):
        return []
    return [json.loads(x) for x in open(path, encoding="utf-8-sig") if x.strip()]


def bucket(load):
    return "<20" if load < 20 else "20-40" if load < 40 else "40-80" if load < 80 else ">=80"


def analyze(run, variant, text_id, logs):
    typed = TEXTS[text_id]
    expect = typed[: typed.index("\n") + 1] if "\n" in typed else typed
    entries = sorted(read(os.path.join(logs, f"{run}.sw.jsonl")) + read(os.path.join(logs, f"{run}.cs.jsonl")),
                     key=lambda e: e["t"])
    shift = False
    hid = ""
    hid_times = []
    for e in entries:
        if e["kind"] not in ("hidDown", "hidUp"):
            continue
        code = int(e["text"].split()[0])
        if code in SHIFT:
            shift = e["kind"] == "hidDown"
        elif e["kind"] == "hidDown":
            hid += decode(code, shift)
            hid_times.append(e["t"])
    fields = [e for e in entries if e["kind"] in ("ui", "uiChange", "uiUnfocused")]
    field = fields[-1]["text"] if fields else ""
    delivered_kind = "swState" if variant == "state" else "csSet"
    delivered = [e for e in entries if e["kind"] == delivered_kind]
    models = [e for e in entries if e["kind"] == ("swState" if variant == "state" else "model")]
    model = models[-1]["text"] if models else ""
    if variant == "state" and not models:
        model = field
    gaps = [b - a for a, b in zip(hid_times, hid_times[1:])]
    info = {
        "typed": typed, "expect": expect, "hid": hid, "field": field, "model": model,
        "hid_ok": hid.startswith(expect) or hid == typed,
        "kb": ",".join(sorted({e["kind"] + (":" + e["text"] if e["kind"] == "hwKeyboard" else "")
                               for e in entries if e["kind"] in ("kbShow", "kbHide", "hwKeyboard")})),
        "rate": (f"{1000 * (hid_times[-1] - hid_times[0]) / max(1, len(hid_times) - 1):.1f}ms/key"
                 f" min={1000 * min(gaps):.1f}" if gaps else "-"),
        "delivered": len(delivered),
    }
    state = "ok"
    if field != expect or model != expect:
        actual = field if field != expect else model
        if expect.startswith(actual) and model in (field, expect):
            state = "cut"
        else:
            state = "LOST"
        i = next((k for k, (a, b) in enumerate(zip(expect, actual)) if a != b), min(len(expect), len(actual)))
        prefix = expect[: i + 1]
        if not hid.startswith(prefix):
            layer = "not-in-hid"
        elif not any(e["text"].startswith(prefix) for e in fields):
            layer = "hid-not-in-field"
        elif not any(e["text"].startswith(prefix) for e in delivered):
            layer = "field-not-delivered"
        else:
            layer = "delivered-then-lost"
        info["where"] = f"index {i} ({expect[i] if i < len(expect) else 'end'!r}) -> {layer}"
    info["state"] = state
    return info


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--one")
    p.add_argument("out")
    p.add_argument("--details", action="store_true")
    a = p.parse_args()
    logs = os.path.join(a.out, "logs")
    table = defaultdict(lambda: [0, 0, 0])
    loads = defaultdict(lambda: [0, 0])
    details = []
    for raw in open(os.path.join(a.out, "results.txt"), encoding="utf-8"):
        m = LINE.search(raw)
        if not m:
            continue
        run, tag, variant, mode, text_id, l0, l1, _secs, yes = m.groups()
        if a.one and run != a.one:
            continue
        info = analyze(run, variant, text_id, logs)
        load = (float(l0) + float(l1)) / 2
        key = (variant, mode, text_id)
        table[key][0] += 1
        table[key][1] += info["state"] == "LOST"
        table[key][2] += info["state"] == "cut"
        loads[(variant, bucket(load))][0] += 1
        loads[(variant, bucket(load))][1] += info["state"] == "LOST"
        details.append((run, variant, mode, text_id, f"load={load:.1f}", f"yes={yes}", info["state"],
                        info.get("where", ""), f"hid_ok={info['hid_ok']}", info["rate"], f"edits={info['delivered']}",
                        info["kb"], f"field={info['field']!r}", f"model={info['model']!r}",
                        f"hid={info['hid']!r}" if not info["hid_ok"] or info["state"] != "ok" else ""))
    if a.one:
        for row in details:
            print(*row)
        return
    print("variant mode text runs lost cut")
    for key in sorted(table):
        print(*key, *table[key])
    print("\nvariant load-bucket runs lost")
    for key in sorted(loads):
        print(*key, *loads[key])
    if a.details:
        print("\nruns")
        for row in details:
            print(*row)


if __name__ == "__main__":
    main()
