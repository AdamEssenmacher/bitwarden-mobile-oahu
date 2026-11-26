#!/usr/bin/env bash
# Runs one set of the issue 977 harness in OAHU's EditableLab (patched with editablelab-repro977.patch, built with
# xcodebuild build-for-testing into $DD) on simulator $UDID, then copies the app's per-run logs into $OUT.
# Usage: run-editablelab.sh <tag> <rounds> <variants> <modes> [synthetic-load-processes]
# With a fifth argument N > 0, N `yes > /dev/null` processes run for the duration of the set and are killed after.
set -euo pipefail
tag="$1" rounds="$2" variants="$3" modes="$4" burn="${5:-0}"
: "${UDID:?}" "${DD:?}" "${LAB:?}" "${OUT:?}"
mkdir -p "$OUT"
pids=()
cleanup() { for p in ${pids[@]+"${pids[@]}"}; do kill "$p" 2>/dev/null || true; done; }
trap cleanup EXIT
for ((i = 0; i < burn; i++)); do yes > /dev/null & pids+=($!); done
echo "set $tag start load $(sysctl -n vm.loadavg) burn=$burn" | tee -a "$OUT/sets.txt"
# Hard cap: about 12 s a run, doubled.
cap=$(( rounds * $(tr ',' '\n' <<<"$variants" | wc -l) * $(tr ',' '\n' <<<"$modes" | wc -l) * 25 + 120 ))
TEST_RUNNER_REPRO_ROUNDS="$rounds" TEST_RUNNER_REPRO_VARIANTS="$variants" TEST_RUNNER_REPRO_MODES="$modes" \
TEST_RUNNER_REPRO_TAG="$tag" timeout "$cap" xcodebuild test-without-building -project "$LAB/EditableLab.xcodeproj" \
    -scheme EditableLab -destination "platform=iOS Simulator,id=$UDID" -derivedDataPath "$DD" \
    -only-testing:EditableLabUITests/Repro977UITests -parallel-testing-enabled NO > "$OUT/$tag.xcodebuild.log" 2>&1 || \
    echo "xcodebuild exited $? for $tag" | tee -a "$OUT/sets.txt"
cleanup; pids=()
echo "set $tag end load $(sysctl -n vm.loadavg)" | tee -a "$OUT/sets.txt"
grep -E '^REPRO977 RESULT' "$OUT/$tag.xcodebuild.log" >> "$OUT/results.txt" || true
container="$(xcrun simctl get_app_container "$UDID" com.adamessenmacher.oahu.EditableLab data)"
mkdir -p "$OUT/logs"
cp "$container"/Documents/repro977/"$tag"-*.jsonl "$OUT/logs/" 2>/dev/null || true
