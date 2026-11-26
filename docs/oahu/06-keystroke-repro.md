# Step 6: keystroke-loss reproduction (OAHU issue 977, gap G34)

A controlled reproduction of G34. In step 5, fast typing into the multi-line notes field (`TextField(axis: .vertical)`
bound to the generated `$model.notes`) lost one character in 3 of 4 runs. This step tried to reproduce that with
scripted typing, several bindings, two hosts and logs at every layer. Nothing was fixed.

## Result

No characters were lost in 708 runs on two hosts: plain SwiftUI bindings, the generated OAHU binding (multi-line and
single-line), a view model that normalizes every edit (the hold starts on every keystroke) and a slow view model. The
load average ranged from 5 to over 200. A positive control that publishes stale echoes lost characters in 6 of 10
runs, and the logs put the loss where it was made. So the harness can see a loss.

In the Bitwarden form, the view model's answer equalled the edit for all 12,266 notes edits. The hold
(`OahuEditableText.Hold`) never started there, so it cannot have caused G34's losses.

The G34 losses are not reproduced, and the cause is still unknown. The typing path that was not tried is the closest
match to the original: the Simulator app's hardware keyboard (key events from the Mac, through Simulator.app). Devices
were not tried.

## Method

- **Simulator:** "BW OAHU Repro977 5140", an iPhone 16 Pro on iOS 18.6 (the step 5 runtime), created for this step
  and deleted after. It was headless: no Simulator.app window.
- **Typing:** an XCUITest bundle (added to OAHU's EditableLab by [`editablelab-repro977.patch`](06-keystroke-repro/editablelab-repro977.patch))
  launches the app once per run, taps the field and types `The quick brown fox jumps over` (30 characters):
  - `xcui0`: `typeText` right after the tap. It goes through the software keyboard at about 60 characters a second (15–17 ms
    between edits in the logs).
  - `xcui1`: the tap, a 1 s pause, then `typeText`.
  - `inject`: the app calls `UIKeyInput.insertText` on the focused field every 50 ms (20 a second), from the first frame
    it sees focus. This leaves XCUITest and the keyboard out.
  
  One Bitwarden set typed `Line one`, the step 5 text, with `xcui0`.
- **Outcome:** after typing, the harness waits for the field to settle, then reads the field's final UIKit text and the
  model's settled value (`titleTextInput` / the last C# edit). A run is lost when either differs from what was typed.
- **Layers logged** (sequence numbers, one uptime clock for Swift and C#):
  - `key`: a character the app injected.
  - `uiChange`: UIKit's text-did-change notification.
  - `ui`: the focused field's UIKit text, sampled every frame and logged when it changes, so programmatic rewrites show.
  - `edit` / `csSet`: what the binding setter delivered to the view model.
  - `answer` / `csGet`: what the view model published back.
  - `model`: the generated model's settled value, sampled every frame.

  For a lost run, [`analyze.py`](06-keystroke-repro/analyze.py) and
  [`analyze-bitwarden.py`](06-keystroke-repro/analyze-bitwarden.py) find the first character that differs. They report
  where it got to: never in the field, in the field but never delivered, or delivered and then overwritten.
- **Interleaving:** each set runs every variant and mode in each round. The variant order rotates by one each round.
- **Load:** the 1-minute load average before and after each run (`getloadavg`, the same as the host's
  `sysctl vm.loadavg`, since the simulator shares the host kernel). The tables bucket runs by the mean of the two
  values. Other sessions were using the machine throughout. My own simulator and test runner add to the load too.

### Hosts and variants

**EditableLab**: OAHU 104c58c2, `tests/native/apple/EditableLab`, arm64, no MAUI, no .NET. The generated
`EverythingModel.title` editable over its preview port: the same generated Swift and runtime (`OahuConnection.edit`,
`OahuEditableText.Hold`, the byte round trip), with Swift handlers in place of C#.

| Variant | Binding |
|---|---|
| A | plain SwiftUI `@State`, `axis: .vertical` (no OAHU) |
| O | plain `@Observable` class, `axis: .vertical` (no OAHU) |
| B | `$model.title`, `axis: .vertical`, the view model accepts as typed |
| C | `$model.title`, single line, accepts |
| D | `$model.title`, `axis: .vertical`, the view model upper-cases: every answer differs, so the hold starts on every edit |
| S | `$model.title`, `axis: .vertical`, accepts after a 30 ms main-thread stall per edit |
| L | positive control: accepts, then re-publishes each proposal 30 ms later (a stale echo) |

**Bitwarden**: this branch, Debug, `iossimulator-x64` under Rosetta, the DEBUG `add-note` route. The test sets
`BW_OAHU_NOTES_MODE`, which this branch adds to `CipherAddEdit.oahu-screen.swift`:

| Variant | Binding of the notes field |
|---|---|
| state | a plain `@State`, `axis: .vertical` (no OAHU; the model never sees the text) |
| model | `$model.notes`, `axis: .vertical` (step 5's default) |
| local | step 5's `localNotesBinding` (`BW_OAHU_NOTES_LOCAL=1`) |
| single | `$model.notes`, single line |

Probes, all inactive unless `BW_OAHU_REPRO_RUN` is set: `OahuKeyProbe.swift` (Swift) and `OahuDebugKeyLog.cs` (C#,
DEBUG only). `CipherText` for Notes logs each read and write under `#if DEBUG`.

## Counts

"Runs/lost" per load bucket. All results files and logs are in [`06-keystroke-repro/results`](06-keystroke-repro/results)
(the logs are in `logs.tar.gz`).

### EditableLab (arm64, no MAUI)

The main sets (`amb1`–`amb4`, 20 runs per cell) unintentionally had 2 `yes` processes running (a runner bug, fixed
before the later sets; on 18 cores that adds about 2 to the load average). `clean1` had none. `burn1` had 8
deliberate `yes` processes, killed after the set.

| Variant | Runs (amb, 2 `yes`) | Lost | load <20 | 20–40 | 40–80 | ≥80 | Runs (clean1) | Lost | Runs (burn1, 8 `yes`) | Lost |
|---|---|---|---|---|---|---|---|---|---|---|
| A `@State` | 60 | 0 | 8/0 | 23/0 | 14/0 | 15/0 | 6 | 0 | 8 | 0 |
| O `@Observable` | 60 | 0 | 13/0 | 17/0 | 15/0 | 15/0 | 6 | 0 | – | – |
| B generated, vertical | 60 | 0 | 9/0 | 14/0 | 20/0 | 17/0 | 6 | 0 | 8 | 0 |
| C generated, single line | 60 | 0 | 6/0 | 12/0 | 27/0 | 15/0 | 6 | 0 | 8 | 0 |
| D normalizing (hold every edit) | 60 | 0 | 9/0 | 15/0 | 23/0 | 13/0 | 6 | 0 | 8 | 0 |
| S slow view model | 60 | 0 | 8/0 | 14/0 | 21/0 | 17/0 | 6 | 0 | – | – |
| L stale echo (control) | 10 | **6** | – | – | – | 10/6 | – | – | – | – |

Each amb cell is 20 `xcui0`, 20 `xcui1` and 20 `inject` runs (clean1 and burn1: `xcui0` and `inject` only; see the
per-mode tables printed by `tabulate.py`). The per-run load averages ranged from 10 to 515, with a median of 46.
Variant D started the hold about 54 times per run (4,032 hold starts in 74 runs) and lost nothing. The control lost
characters in 5 of 5 `xcui0` runs and 1 of 5 `inject` runs: the 50 ms gap between injected keys is longer than the
30 ms echo, so most echoes were not stale.

### Bitwarden (MAUI, x86_64 under Rosetta)

| Variant | Runs | Lost | load <20 | 20–40 | 40–80 | ≥80 | `Line one`, `xcui0` | 8 `yes` (burnbw1) |
|---|---|---|---|---|---|---|---|---|
| state | 61 | 0 | 30/0 | 17/0 | 9/0 | 5/0 | 5/0 | 6/0 |
| model | 61 | 0 | 35/0 | 10/0 | 5/0 | 11/0 | 5/0 | 6/0 |
| local | 60 | 0 | 38/0 | 5/0 | 11/0 | 6/0 | 5/0 | – |
| single | 60 | 0 | 34/0 | 9/0 | 8/0 | 9/0 | 5/0 | 6/0 |

Each main cell is about 20 runs per mode (`xcui0`, `xcui1`, `inject`; the `state` and `model` counts include one
smoke run each). The per-run load averages ranged from 5 to 222, with a median of 15. The machine was quieter than
during the EditableLab sets.

- The first set's 20 `inject` runs were invalid and are left out (`bwresults-invalid-bw1-inject.txt`). The add form
  focuses the name field when it opens, so the probe typed into the name field before the test tapped the notes. The
  probe now waits for SwiftUI focus on the notes field, and `bw5` re-ran those 20.
- The first set's `xcui` runs used the earlier probe. That changes only what is logged before the tap, not the
  outcome.
- Two `state` runs show the `@State` log (`.onChange`) missing the last characters, while the field kept the whole
  text. That is a gap in the logging, not a loss: the `@State` has no other reader to check against.

From the C# log: 12,266 notes edits reached C#, and every published answer equalled the edit, so the hold never
started. Every C# entry ran on the main thread. The vertical field still sends two edits per keystroke, plus two `""`
edits when it takes focus, as step 5 saw.

For the step 5 case (`model`, multi-line) there were 72 runs and none lost a character. With 0 losses in 72, a loss
rate above about 4% per run (1 in 24) is unlikely (95%, rule of three). Step 5 saw 3 losses in 4 runs.

## Log excerpts

**Positive control (EditableLab, L, `xcui0`): delivered, then overwritten by a stale echo.** The setter got `Th` and
`The` and the field showed `The`. Then the echoes of `T` and `Th` arrived and rewrote the field. The next keystroke was
typed into the rewritten text. The run ended as `T quichk brown fox jumpsover`, and `analyze.py` reports
"setter-then-lost":

```
{"seq":15,"ms":3012.5,"kind":"edit","text":"Th"}
{"seq":20,"ms":3013.5,"kind":"edit","text":"The"}
{"seq":24,"ms":3013.6,"kind":"uiChange","text":"The"}
{"seq":26,"ms":3026.6,"kind":"lateEcho","text":"T"}      <- stale value published
{"seq":28,"ms":3043.5,"kind":"lateEcho","text":"Th"}
{"seq":30,"ms":3044.9,"kind":"uiChange","text":"T h"}    <- next key lands in the rewritten text
{"seq":31,"ms":3072.6,"kind":"edit","text":"T qh"}
```

In one other control run, the first differing character had been typed into text that was already rewritten. The
analyzer called that run "never in the field". The rule looks only at the first differing character, so read its
labels next to the log.

**Hold engaged, nothing lost (EditableLab, D, `xcui0`).** Every answer differs from the edit (`Th` → `TH`), so the
hold starts on every keystroke. The run ends with the field and the model both `THE QUICK BROWN FOX JUMPS OVER`:

```
{"seq":13,"ms":3067.5,"kind":"edit","text":"Th"}
{"seq":14,"ms":3067.5,"kind":"answer","text":"TH"}
...
{"seq":188,"ms":5383.7,"kind":"summary","text":"field=THE QUICK BROWN FOX JUMPS OVER|model=THE QUICK BROWN FOX JUMPS OVER"}
```

**Bitwarden, model, `xcui0`.** These are C# and Swift entries on one clock. Each keystroke reaches C# twice, and each
answer equals the edit:

```
735.3493 csSet 'T'   735.3494 csGet 'T'   735.3504 csSet 'T'   735.3505 csGet 'T'   735.3523 uiChange 'T'
735.4015 csSet 'Th'  735.4016 csGet 'Th'  735.4018 csSet 'Th'  735.4019 csGet 'Th'  735.4019 uiChange 'Th'
```

## Conclusion

- The G34 losses were not reproduced. With software-keyboard typing (XCUITest) and direct `insertText`, on an iOS 18.6
  simulator, no binding lost a character in either host. That includes the generated binding, multi-line, typing right
  after focus, under MAUI on x86_64/Rosetta, and with the load average past 200 (past 500 in EditableLab).
- OAHU's runtime is not implicated by this evidence:
  - In the Bitwarden form, the hold (`OahuEditableText.Hold` in `EditableText.swift`, started by the generated setter)
    never started.
  - Every answer came back on the main thread, before the setter returned (`OahuConnection.edit`).
  - The only way the harness saw text overwritten was a view model that publishes stale values, which Bitwarden's
    doesn't do.
  - A normalizing view model, which does use the hold on every edit, lost nothing.
- Confidence: moderate for the paths tested. This rules out a frequent loss in these paths (step 5's rate was 3 in 4),
  not a rare one.
- It says nothing about the path step 5 used: the Simulator app's hardware keyboard, with a Simulator window on screen,
  at load 64–146. That path, or the timing of the tool that typed through it, is now the most likely place to look.

## Not tested

- The Simulator.app hardware-keyboard path (key events from the Mac). No run here had a Simulator window or a hardware
  keyboard.
- A physical device. Two paired devices were listed by `xcrun devicectl list devices` (an iPhone SE and an iPad mini).
  They weren't used, because installing on personal devices was out of scope.
- iOS versions other than 18.6, and Release builds.
- A rare loss. Zero in 20 runs bounds one variant-and-mode cell only to under about 15% (95%); zero in 60–72 runs bounds
  one variant to under about 4–5%.
- Return, deletes, autocorrect replacements and marked text. Autocorrect was left on, and the typed words are all real
  words.

## Reproducing

1. In an OAHU worktree at 104c58c2: `git apply docs/oahu/06-keystroke-repro/editablelab-repro977.patch` (from this
   repository), then `bash eng/prepare-native.sh swift`.
2. `xcodebuild build-for-testing -project tests/native/apple/EditableLab/EditableLab.xcodeproj -scheme EditableLab
   -destination "platform=iOS Simulator,id=$UDID" -derivedDataPath $DD`
3. For Bitwarden: build this branch (`dotnet build -f net11.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-x64` in
   `src/App`) and `xcrun simctl install $UDID .../App.app`.
4. `UDID=… DD=… LAB=<worktree>/tests/native/apple/EditableLab OUT=<dir> bash run-editablelab.sh <tag> <rounds> A,O,B,C,D,S xcui0,xcui1,inject [yes-count]`
   (or `run-bitwarden.sh … state,model,local,single …`). Then run `tabulate.py <OUT>/results.txt` and
   `analyze.py <OUT>/results.txt <OUT>/logs --details`.
