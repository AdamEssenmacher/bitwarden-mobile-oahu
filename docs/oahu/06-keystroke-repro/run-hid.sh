#!/usr/bin/env bash
# Round 2 (hardware-keyboard path) of the issue 977 harness. The keys themselves are sent by the agent between `pre`
# and `post`, with the iOS Simulator control tool's `tap` and `text` actions (HID events addressed to $UDID); this
# script only launches the app, records the load and collects the logs.
#   run-hid.sh pre  <run> <variant> [autocorrect: 1|0]   load0, relaunch the DEBUG add-note route, wait for the screen
#   run-hid.sh post <run> <variant> <mode> <text-id>      wait to settle, load1, copy logs, append a RESULT line
#   run-hid.sh burn <n> | unburn                          bounded synthetic load (n `yes` processes)
set -euo pipefail
: "${UDID:?}" "${OUT:?}"
mkdir -p "$OUT/logs"
now() { perl -MTime::HiRes=time -e 'printf "%.3f", time'; }
load() { sysctl -n vm.loadavg | awk '{print $2}'; }
container() { xcrun simctl get_app_container "$UDID" com.8bit.bitwarden data; }
case "$1" in
pre)
    run="$2" variant="$3" ac="${4:-1}"
    echo "$(load) $(now)" > "$OUT/logs/$run.pre"
    xcrun simctl terminate "$UDID" com.8bit.bitwarden 2>/dev/null || true
    rm -f "$(container)/Documents/repro977/$run".*
    SIMCTL_CHILD_BW_OAHU_DEBUG=edit SIMCTL_CHILD_BW_OAHU_EDIT=add-note SIMCTL_CHILD_BW_OAHU_NOTES_MODE="$variant" \
        SIMCTL_CHILD_BW_OAHU_REPRO_RUN="$run" SIMCTL_CHILD_BW_OAHU_REPRO_MODE=hid \
        SIMCTL_CHILD_BW_OAHU_NOTES_AUTOCORRECT="$ac" \
        timeout 60 xcrun simctl launch "$UDID" com.8bit.bitwarden > /dev/null
    for _ in $(seq 1 120); do
        [ -f "$(container)/Documents/repro977/$run.sw.jsonl" ] && break
        sleep 0.5
    done
    [ -f "$(container)/Documents/repro977/$run.sw.jsonl" ] || { echo "no probe log for $run"; exit 1; }
    sleep 1.5   # the form focuses the name field when it opens; let that settle before the tap
    echo "ready $run load0=$(cut -d' ' -f1 "$OUT/logs/$run.pre")"
    ;;
post)
    run="$2" variant="$3" mode="$4" textid="$5"
    sleep 3.5   # the probes write their logs 1 s after their last entry
    l0=$(cut -d' ' -f1 "$OUT/logs/$run.pre") t0=$(cut -d' ' -f2 "$OUT/logs/$run.pre")
    l1=$(load)
    secs=$(echo "$(now) - $t0" | bc)
    cp "$(container)/Documents/repro977/$run".*.jsonl "$OUT/logs/" 2>/dev/null || true
    echo "REPRO977 RESULT run=$run tag=${run%%-*} round=0 variant=$variant mode=$mode text=$textid load0=$l0 load1=$l1 seconds=$secs yes=$(pgrep -x yes | wc -l | tr -d ' ')" \
        | tee -a "$OUT/results.txt"
    python3 "$(dirname "$0")/analyze-hid.py" --one "$run" "$OUT" || true
    ;;
burn)
    n="$2"
    for ((i = 0; i < n; i++)); do nohup yes > /dev/null 2>&1 & echo $! >> "$OUT/yes.pids"; done
    echo "burn $n started; yes now $(pgrep -x yes | wc -l | tr -d ' ') load $(load)" | tee -a "$OUT/sets.txt"
    ;;
unburn)
    [ -f "$OUT/yes.pids" ] && xargs kill < "$OUT/yes.pids" 2>/dev/null || true
    rm -f "$OUT/yes.pids"
    sleep 0.5
    echo "unburn; yes now $(pgrep -x yes | wc -l | tr -d ' ') load $(load)" | tee -a "$OUT/sets.txt"
    ;;
esac
