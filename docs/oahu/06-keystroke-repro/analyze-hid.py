#!/usr/bin/env python3
"""Round 2 (hardware-keyboard path) analyzer for the issue 977 Bitwarden runs written by run-hid.sh.

Usage: analyze-hid.py <out dir> [--details]      every run in <out dir>/results.txt
       analyze-hid.py --one <run> <out dir>       one run, printed after it is collected

Per run it reads <run>.sw.jsonl (Swift: ui/uiChange/uiUnfocused, model, swState, notesFocused, kbShow/kbHide,
hwKeyboard, and GameController hidDown/hidUp, which stay empty in the simulator), <run>.cs.jsonl (C#: csSet, csGet),
<run>.bb.ndjson (backboardd's KeyPresses log: every key it delivered and the receiving pid), <run>.clock and
<run>.launch (the app's pid), all put on the probes' uptime clock.

- typed: the text the control tool was asked to type (TEXTS below, by text id).
- hid: the keys backboardd delivered to the app's pid, decoded from HID usage codes (letters, space, Return).
- field: the field's last UIKit text (ui, uiChange or uiUnfocused, whichever is last).
- model: the generated model's last settled notes. For `state`, the field (the @State has no reader but the
  .onChange log, which can lag; a lag is noted as swState-lag).

A hardware Return ends editing of the vertical notes field (step 5, G34), so the text after the first Return does not
reach it. The run is scored on `expect`: typed up to and including the first Return for texts with Return (without
it for `single`, which submits on Return), else all of it. A run is "lost" when the field or the model differs from `expect`, and "cut" when the field is a strict prefix
of `expect` (keys stopped arriving; not a dropped character inside the text). For the first differing character the
layer is: not-in-hid (the app never received the key), hid-not-in-field (received, never in the UIKit text),
field-not-delivered (in the field, never in a setter value), delivered-then-lost (delivered, later overwritten).
"""
import argparse
import json
import os
import re
from collections import defaultdict
from datetime import datetime

TEXTS = {
    "fox": "The quick brown fox jumps over",
    "line5": "Line one\nLine two\nLine three\nLine four\nLine five teh",
    "line3": "Line one\nLine two\nLine three",
    "line1": "Line one\n",
    "alpha": "Alpha beta gamma delta epsilon",
}
LINE = re.compile(r"run=(\S+) tag=(\S+) round=\d+ variant=(\S+) mode=(\S+) text=(\S+) load0=(\S+) load1=(\S+) "
                  r"seconds=(\S+) yes=(\d+)")
SHIFT = {225, 229}
BB = re.compile(r"KeyPress page:0x7 usage:0x([0-9A-Fa-f]+) downEvent:(\d) \w+ -> <keyboardFocus;[^>]*pid: (\d+)")


def backboardd(logs, run):
    """The keys backboardd delivered, as (uptime, usage, down, pid), from <run>.bb.ndjson (log stream) and <run>.clock
    (wall clock and uptime read together, to put the log's wall-clock timestamps on the probes' clock)."""
    path = os.path.join(logs, f"{run}.bb.ndjson")
    clock = os.path.join(logs, f"{run}.clock")
    if not os.path.exists(path) or not os.path.exists(clock):
        return None
    wall, up = map(float, open(clock).read().split())
    out = []
    for line in open(path, encoding="utf-8", errors="replace"):
        if not line.startswith("{"):
            continue
        e = json.loads(line)
        m = BB.search(e.get("eventMessage", ""))
        if not m:
            continue
        ts = datetime.strptime(e["timestamp"], "%Y-%m-%d %H:%M:%S.%f%z").timestamp()
        out.append((ts - wall + up, int(m.group(1), 16), m.group(2) == "1", int(m.group(3))))
    return out


def app_pid(logs, run):
    path = os.path.join(logs, f"{run}.launch")
    m = re.search(r": (\d+)", open(path).read()) if os.path.exists(path) else None
    return int(m.group(1)) if m else None


def decode(code, shift):
    if 4 <= code <= 29:
        c = chr(ord("a") + code - 4)
        return c.upper() if shift else c
    return {44: " ", 40: "\n", 42: "<bs>"}.get(code, f"<{code}>")


def read(path):
    if not os.path.exists(path):
        return []
    return [json.loads(x) for x in open(path, encoding="utf-8-sig") if x.strip()]


def ui_rate(entries, expect):
    changes = [e for e in entries if e["kind"] == "uiChange" and e["text"]]
    end = next((e for e in changes if e["text"] == expect), changes[-1] if changes else None)
    if not changes or end is changes[0]:
        return "ui=-"
    n = max(1, len(end["text"]) - len(changes[0]["text"]))
    return f"ui={1000 * (end['t'] - changes[0]['t']) / n:.1f}ms/char"


def bucket(load):
    return "<20" if load < 20 else "20-40" if load < 40 else "40-80" if load < 80 else ">=80"


def analyze(run, variant, text_id, logs):
    typed = TEXTS[text_id]
    expect = typed[: typed.index("\n") + 1] if "\n" in typed else typed
    if variant == "single":
        expect = expect.rstrip("\n")  # a single-line field submits on Return and inserts no newline
    entries = sorted(read(os.path.join(logs, f"{run}.sw.jsonl")) + read(os.path.join(logs, f"{run}.cs.jsonl")),
                     key=lambda e: e["t"])
    # The HID layer is backboardd's log (keys delivered to a pid). The probe's GameController keys (hidDown/hidUp) are
    # a cross-check only: GCKeyboard is nil in the simulator.
    pid = app_pid(logs, run)
    bb = backboardd(logs, run)
    keys = [(t, code, down, p) for t, code, down, p in (bb or [])]
    gc = [(e["t"], int(e["text"].split()[0]), e["kind"] == "hidDown", pid) for e in entries
          if e["kind"] in ("hidDown", "hidUp")]
    shift = False
    hid = ""
    hid_times = []
    other_pid = 0
    for t, code, down, p in keys:
        if p != pid:
            other_pid += down
            continue
        if code in SHIFT:
            shift = down
        elif down:
            hid += decode(code, shift)
            hid_times.append(t)
    gc_hid = ""
    shift = False
    for t, code, down, p in gc:
        if code in SHIFT:
            shift = down
        elif down:
            gc_hid += decode(code, shift)
    focus_t = next((e["t"] for e in entries if e["kind"] == "notesFocused" and e["text"] == "true"), None)
    first_ui = next((e["t"] for e in entries if e["kind"] in ("uiChange",) and e["text"]), None)
    fields = [e for e in entries if e["kind"] in ("ui", "uiChange", "uiUnfocused")]
    field = fields[-1]["text"] if fields else ""
    delivered_kind = "swState" if variant == "state" else "csSet"
    delivered = [e for e in entries if e["kind"] == delivered_kind]
    models = [e for e in entries if e["kind"] == ("swState" if variant == "state" else "model")]
    model = models[-1]["text"] if models else ""
    swstate_lag = variant == "state" and models and model != field
    if variant == "state":
        # The @State's only reader is the .onChange log, which can miss the last edits while the field has them (round
        # 1); the plain @State has no other reader, so score the field and note the lag.
        model = field
    gaps = [b - a for a, b in zip(hid_times, hid_times[1:])]
    info = {
        "typed": typed, "expect": expect, "hid": hid, "field": field, "model": model,
        "hid_ok": hid.startswith(expect) or hid == typed,
        "bb": "none" if bb is None else f"{len(hid)}keys" + (f"+{other_pid}other" if other_pid else ""),
        "gc": len(gc_hid),
        "focus_to_key": (f"{1000 * (hid_times[0] - focus_t):.0f}ms" if hid_times and focus_t else
                         "nofocus" if not focus_t else "-"),
        "key_to_text": f"{1000 * (first_ui - hid_times[0]):.0f}ms" if hid_times and first_ui else "-",
        "final": fields[-1]["text"] if fields else "",
        "kb": ",".join(sorted({e["kind"] + (":" + e["text"] if e["kind"] == "hwKeyboard" else "")
                               for e in entries if e["kind"] in ("kbShow", "kbHide", "hwKeyboard")})),
        # backboardd hands the app the whole string within a few ms; the app then works through its queue. "burst" is
        # backboardd's span for all keys, "ui" the mean time per field change up to the first Return (or the end).
        "rate": (f"burst={1000 * (hid_times[-1] - hid_times[0]):.0f}ms" if gaps else "burst=-") + " " + ui_rate(
            entries, expect),
        "delivered": len(delivered),
        "note": "swState-lag" if swstate_lag else "",
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
                        f"bb={info['bb']}", f"gc={info['gc']}", f"focus->key={info['focus_to_key']}",
                        f"key->text={info['key_to_text']}", info["note"],
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
