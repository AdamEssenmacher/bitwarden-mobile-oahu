# Step 6: keystroke-loss reproduction (OAHU issue 977, gap G34)

A controlled reproduction of G34. In step 5, fast typing into the multi-line notes field (`TextField(axis: .vertical)`
bound to the generated `$model.notes`) lost one character in 3 of 4 runs. This step tried to reproduce that with
scripted typing, several bindings, two hosts and logs at every layer. Nothing was fixed.

Round 1 (software keyboard, `insertText`) did not reproduce it. [Round 2](#round-2-hardware-keyboard-path) typed
through hardware-keyboard (HID) events, as step 5 did, and did reproduce it. The cause is in UIKit/SwiftUI, not OAHU:
a hardware Return ends editing of the vertical field while an earlier key is still being inserted, and that key is
dropped. The plain `@State` control loses characters as often.

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

## Round 2: hardware-keyboard path

Round 2 typed through the path step 5 used: HID key events sent to the simulator. It reproduced the loss, and the
plain `@State` control (no OAHU) loses characters too. The character is lost inside UIKit: the app receives the key,
and it never reaches the field's text.

### Result

- The step 5 result came back: `Line ne\n` for `Line one\nLine two…`, the same string step 5's first run saved. The
  multi-line field lost one character in 30 of the 78 runs that typed a Return in the same burst as the letters before
  it.
- **The no-OAHU control loses at least as often.** In the primary condition, the plain `@State` field lost a character
  in 10 of 20 runs; the generated binding in 6 of 20; step 5's local binding in 4 of 10. The difference between
  `state` and `model` is not significant (Fisher p = 0.33). The single-line field (Return submits) lost none in 20.
- **Where:** every one of the 30 losses is "received by the app, never in the field". backboardd delivered the key to
  the app's pid, and no UIKit text-change ever contained it. UIKit's own log shows why. The Return keypress ends editing
  of the vertical field (`_teardownExistingDelegate:<SwiftUI.VerticalTextView>`) as soon as UIKit dispatches it. The
  keyboard inserts typed text through an asynchronous pipeline that runs behind event dispatch. A key that is inside
  that pipeline when editing ends is dropped. Keys already queued behind it are still inserted.
- **Not OAHU.** The loss happens before any binding sees the text, and with a plain `@State` as well. The generated
  setter got `Line ` for the dropped key (SwiftUI syncs the field's text when editing ends) and never `Line o`. The
  view model echoed every edit unchanged, as in round 1. OAHU's runtime (`OahuConnection.edit`,
  `OahuEditableText.Hold`) is not implicated.
- **What it needs:** a hardware Return in the vertical field while earlier keys are still queued. The HID burst does
  that: the control tool hands backboardd the whole string in 10–30 ms, and the app works through it at about 10 ms a
  character. Without a Return (`fox`) nothing was lost (0 of 10). With the Return sent on its own after the letters
  had landed (K2), no letter was lost (0 of 6); the Return ended editing without inserting a newline. With the Return
  in the same call as `one` (K1), the `e` before it was lost in 5 of 6 runs. Waiting 2 s between the tap and the
  typing (W) did not prevent it (2 of 10), nor did turning off autocorrection (A, 2 of 10).
- Confidence: high that this mechanism produces the step 5 symptom, and that it does not need OAHU. Moderate that it
  is the only cause of step 5's losses: step 5's second loss (`Lie one`, the `n` at index 2) was not seen here. The
  same mechanism predicts it if the keyboard's pipeline was further behind; step 5 ran at load 64–146.

### Method

- **The original mechanism** (from step 5's transcript): the Claude desktop app's iOS Simulator control tool. `attach`,
  then per run one `tap` on the notes field and one `text` call with the whole string, `\n` as a hardware Return.
  Texts: `Line one\nLine two\nLine three\nLine four\nLine five teh` typed straight after the tap (first run saved
  `Line ne\n`), `Line one\nLine two\nLine three` after a 2 s pause, and `Alpha beta gamma delta epsilon` (intact).
- **This round:** the same tool on a new simulator, "BW OAHU Repro977 HID" (iPhone 16 Pro, iOS 18.6), with this
  branch's Debug build (x86_64 under Rosetta) and the DEBUG `add-note` route. Per run: `run-hid.sh pre` (record the
  load, relaunch with the variant, start the backboardd log), then `tap` (80, 497) and `text`, then `run-hid.sh post`
  (wait, record the load, collect the logs, analyze). A one-time keyboard onboarding sheet ("slide to type") was
  dismissed once before the runs.
- **Conditions:**
  - P (primary): `tap` and `text` sent together, with the 52-character `line5` text. The tap lands about 100 ms after
    the keys reach backboardd; UIKit handles the touch first, so the field has focus before the first key is
    inserted.
  - W: `tap`, then `text` in the next call. Focus came 1.8–2.9 s before the keys.
  - F: as P, with `The quick brown fox jumps over` (no Return).
  - A: as P, with autocorrection off (`BW_OAHU_NOTES_AUTOCORRECT=0`).
  - K1: `tap` with `Line `, then `one\n` in a second call. K2: `tap` with `Line one`, then `\n` alone. These were the
    word-by-word runs, split to put the Return in or out of a burst.
- **Variants:** as round 1. `state` (plain `@State`, the no-OAHU control), `model` (the generated binding, multi-line;
  step 5's case), `single` (generated binding, single line), `local` (step 5's local binding). P rounds rotated the
  variant order; the other conditions alternated `state` and `model`.
- **Scoring** ([`analyze-hid.py`](06-keystroke-repro/analyze-hid.py)): a run is scored up to and including the first
  Return. The text after it never reaches the field, because the Return ends editing (G34). For `single` the expected
  text has no newline, because the field submits on Return. "Lost" means a typed letter is missing from the field or
  the model. "Cut" means the field is the typed text without its final newline (all letters intact). For `state` the
  field is scored: the `@State`'s `.onChange` log lagged once (F-r2) while the field was right, as in round 1.
- **Layers:** backboardd (`log stream`, `KeyPresses`: each key with the receiving pid) → UIKit text (`uiChange`,
  sampled `ui`) → setter (`csSet`) → model. In two diagnostic runs (D), UIKit's keyboard log was streamed as well.

### Harness changes

- The prepared HID probe was blind. The probe's GameController handler logged nothing: `GCKeyboard.coalesced` is nil
  in the simulator and `GCKeyboardDidConnect` never fires, even though the keys are real HID events. So `hidDown` is
  empty in every run, and the analyzer would have called every loss "never received by the app". The HID layer is now
  backboardd's own log, which names every key it delivers and the pid it delivers to. `run-hid.sh pre` starts it and
  `post` stops it. The clocks are aligned by reading the wall clock and `CLOCK_UPTIME_RAW` (the probes' clock)
  together.
- The software keyboard stays on screen. The arbiter switches the event source to `KBHW` on the first key, but the
  simulator never reports a hardware keyboard (`hwKeyboard none`).
- Checks before counting: two slow runs (dbg1, dbg2) where the screenshot, the probe and the analyzer agreed
  (`The quick brown fox jumps over`; `Line one` with the keyboard dismissed by the Return). Three edited copies of
  dbg2's logs (a key removed from backboardd's log, an `o` removed from the field and setter, an overwritten model)
  were each classified as expected. In every counted run, backboardd's log has every typed key for the app's pid.
- Each run's focus was checked from the log, not a screenshot. Every counted run's field received text, and the add
  form's initial focus on Name never took a key. Screenshots were taken for the first launches. The notes field sat
  at y = 493–500 points; the tap at y = 497 focused it every time.

### Counts

"Runs/lost". Lost = a letter before the first Return is missing. All results are in
[`06-keystroke-repro/results`](06-keystroke-repro/results): `hid-results.txt`, `hid-analysis.txt` (per run), and the
logs in `hid-logs.tar.gz`.

| Condition | `state` (no OAHU) | `model` (generated, multi-line) | `local` | `single` |
|---|---|---|---|---|
| P: tap + text together, `line5` | **20/10** | **20/6** | 10/4 | 20/0 |
| W: text in the next call (focus 1.8–2.9 s earlier) | 5/2 | 5/0 | – | – |
| A: as P, autocorrection off | 5/1 | 5/1 | – | – |
| F: as P, single-line phrase, no Return | 5/0 | 5/0 | – | – |
| K1: `Line ` then `one\n` | 3/3 | 3/2 | – | – |
| K2: `Line one` then `\n` alone | 3/0 | 3/0 | – | – |
| D: diagnostic (UIKit log on) | 1/1 | 1/0 | – | – |

- The lost character, across all 30 losses: `o` at index 5 (`Line ne\n`) 22 times, `n` at index 6 (`Line oe\n`) 3
  times, and in K1 the `e` before the Return (`Line on\n`) 5 times. In K2 all 6 runs and in K1 one `model` run, the
  Return ended editing without inserting a newline, with every letter intact ("cut" in the analysis).
- P by load bucket (the mean of the 1-minute load average before and after the run):

  | Variant | <20 | 20–40 | 40–80 | ≥80 |
  |---|---|---|---|---|
  | state | 12/5 | 5/3 | 2/2 | 1/0 |
  | model | 15/3 | 2/2 | 1/0 | 2/1 |
  | local | 4/2 | 5/2 | – | 1/0 |
  | single | 14/0 | 5/0 | 1/0 | – |

  The loads came from other sessions; the per-run means ranged from 4.8 to 139, median 15. Losses happen at every
  load, including below 10. No synthetic-load set was run: the machine was not quiet, and its own load already
  covered the range. No `yes` process was running during any run.
- The rates: `state` 10/20 against `model` 6/20 (p = 0.33). `state` against `single` p < 0.001; `model` against
  `single` p = 0.02. Across P, W and A, `state` lost 13 of 30 and `model` 7 of 30 (p = 0.17). The data do not show
  the generated binding losing more often than plain SwiftUI. If anything it loses less often, which fits the
  mechanism: its setter makes each insert slower, so the pipeline is less likely to be mid-key when the Return lands.
  That is not established.

### Log excerpts

**`model`, P-r12 (load 25): received, never in the field.** backboardd delivered all of `Line one\n` (and the rest)
to the app in 2 ms. The field went from `Line ` to `Line n`. The setter's two calls with `Line ` and no text change
are SwiftUI syncing the field when editing ended. `o` never reached UIKit's text, so it never reached C#.

```
231932.6312 BB ' '  231932.6316 BB 'o'  231932.6320 BB 'n'  231932.6324 BB 'e'  231932.6331 BB '\n'   (pid 76685, the app)
231932.7184 notesFocused true
231932.8387 csSet 'Line '  231932.8389 uiChange 'Line '
231932.8466 csSet 'Line '                               <- editing ends; no text change, no 'Line o'
231932.8538 csSet 'Line n' 231932.8540 uiChange 'Line n'
231932.8645 csSet 'Line ne\n' 231932.8653 uiChange 'Line ne\n'
231932.8789 kbHide
```

**`state`, D-r01 (load 26), UIKit's log: the Return ends editing while `o` is in the keyboard's pipeline.** Each key
goes "adds a string" → "sends inputEvent to kbd" → "receives output" → "inserts text". All 20 press events of
`Line one\n` (shift, letters, space, Return; down and up) had been dispatched to the window by 13.268. The teardown
started at 13.270, with 6 keys added and 5 inserted:

```
13.238 Keyboard adds a string            (' ')
13.259 Keyboard inserts text             ('Line ')
13.268 Keyboard adds a string            ('o')
13.270 -_teardownExistingDelegate:<SwiftUI.VerticalTextView ...> forSetDelegate:(nil)   <- Return ends editing
13.274 Keyboard adds a string  13.279 Keyboard inserts text   ('n')
13.283 Keyboard adds a string  13.287 Keyboard inserts text   ('e')
13.289 Keyboard adds a string  13.294 Keyboard inserts text   ('\n')
13.297 -_setDelegate:(nil) existingDelegate:<SwiftUI.VerticalTextView ...>
```

**`model`, D-r02 (load 15), no loss, for contrast.** The same teardown came at the same point, between keys: 6 keys
added and 6 inserted. Nothing was in flight, so nothing was lost.

```
43.259 Keyboard adds a string   43.266 Keyboard inserts text   ('o')
43.271 -_teardownExistingDelegate:<SwiftUI.VerticalTextView ...>
43.275 Keyboard adds a string   43.280 Keyboard inserts text   ('n')
```

### Conclusion

- The hardware-keyboard path reproduces G34's dropped character. In step 5's case, `line5` typed straight after the
  tap, the generated binding lost one in 6 of 20 runs and plain `@State` in 10 of 20.
- The cause is in UIKit/SwiftUI, not OAHU. A hardware Return in a SwiftUI `TextField(axis: .vertical)` ends editing
  when UIKit dispatches the key. Text insertion runs asynchronously behind dispatch, so a key still in the keyboard's
  pipeline at that moment is dropped. The app received the key; it never entered the field's text, so no binding
  could see it. OAHU's runtime is not on the path. The 2.3 edits per keystroke, the setter and the hold play no part.
- It needs a Return typed into the vertical field while earlier keys are still being inserted. A person typing at
  human speed on a hardware keyboard would rarely hit it. A tool or agent that sends a whole string with `\n` in one
  call hits it often. Typing the Return in a separate call avoids it (0 of 6), as does a text without a Return (0 of
  10). It is the same SwiftUI behaviour step 5 recorded: a hardware Return ends editing instead of inserting a
  newline. Here it also drops the key in flight.
- Confidence: high for the mechanism and that OAHU is not needed for it (30 of 30 losses at the same layer, the
  no-OAHU control losing as often, and the UIKit log showing the teardown mid-key). Moderate that it explains every
  step 5 loss: `Lie one` was not reproduced.

### Not tested (round 2)

- A physical device or a Mac's real keyboard through Simulator.app (here the keys came from the control tool's HID
  injection; no Simulator window was in front). Whether a person typing fast on a real keyboard can hit it.
- iOS versions other than 18.6, an arm64 build, a Release build.
- EditableLab: not run, since the no-OAHU control in this host lost characters too.
- A synthetic-load set (`run-hid.sh burn` was not used).
- Why the generated binding's rate is lower than `@State`'s (6/20 against 10/20; not significant).
- What SwiftUI does on the Return in detail (the vertical field's key handling is not public). The log shows the end
  of editing starting right after the Return is dispatched, but not the call that starts it.
