# Step 7: retest on a new OAHU preview

Branch `oahu/07-retest`, cut from `oahu/05-cipher-edit` (83da131f). Goal: move the conversion to a preview of current
OAHU `main`, record what the upgrade broke, and check against the real app whether the changes made for the gaps of
steps 2–5 (G1–G36, issues #931–#979) close them. Findings are in [gaps.md](gaps.md) (issue numbers on every entry,
"retested" lines, and G37–G43); this file is what was done.

OAHU moved from `0.1.0-preview.g104c58c2` (commit 104c58c2) to **`0.1.0-preview.gc63d0aba`** (commit
`c63d0aba6c9ed2201f2c2ab7dd058dd869033604`, origin/main on 2026-11-26 17:24, 49 commits later), packed into
`~/RiderProjects/oahu-feed` with `eng/pack-preview.sh <feed> --commit c63d0aba…` from a throwaway worktree (about
4 minutes; it succeeded first time). Commits that landed on main after the pack (for example #1036 for #978) are not in
this preview.

Effort: about 1 hour 45 minutes of wall-clock time: 5 minutes packing, 10 minutes upgrading, 45 minutes of checks on the
simulator and emulator (including the D25 build-out), 45 minutes writing up and commenting. Host load average 17–711 from
other sessions during the runs (iOS screen updates lagged 1–3 s at the peaks; the Android emulator raised "Bitwarden
isn't responding" twice while its main thread was idle), so timings are indicative only.

## Results

| Target | Build | Run |
|---|---|---|
| `net11.0-ios`, `iossimulator-x64`, Debug | Yes, first time after the upgrade (65 s clean), and after every change below | "BW OAHU Repro977 HID" (iOS 18.6): folders (light, Nord, a live switch to Nord, safe-area A/B), lock (secure mode: show/hide with focus and cursor, wrong password, refocus), add/edit (secure fields, hidden custom field, focus on open with and without the delay, sectioned identity and URIs with Nord) |
| `net11.0-android`, Debug | Yes, first time after the upgrade (95 s), and after every change | **`OAHU_Fold_API36`, read-only, port 5702**, not `OAHU_Pixel_API36`: another session was running `OAHU_Pixel_API36` without `-read-only` the whole time, so the emulator refused a second instance ("Another emulator instance is running. Please close it or run all emulators with -read-only flag."). Folders (Nord, live switch), add/edit sectioned identity (Next into the section, typing, Save, teardown, a fold/unfold with the form open) and URI section (typing in a row) |

Build and run, in addition to step 5's commands:

```sh
# iOS
SIMCTL_CHILD_BW_OAHU_DEBUG=folders SIMCTL_CHILD_BW_OAHU_THEME_SWITCH=nord xcrun simctl launch <udid> com.8bit.bitwarden
SIMCTL_CHILD_BW_OAHU_DEBUG=folders SIMCTL_CHILD_BW_OAHU_THEME=nord SIMCTL_CHILD_BW_OAHU_PAGEBG=1 SIMCTL_CHILD_BW_OAHU_SAFEAREA=0 …
SIMCTL_CHILD_BW_OAHU_DEBUG=edit SIMCTL_CHILD_BW_OAHU_EDIT=edit:identity-alice SIMCTL_CHILD_BW_OAHU_SECTIONS=1 …
SIMCTL_CHILD_BW_OAHU_DEBUG=edit SIMCTL_CHILD_BW_OAHU_EDIT=add-note SIMCTL_CHILD_BW_OAHU_FOCUS=now|held …
#   BW_OAHU_OLDSECURE=1: the step 5 SecureField/TextField swap instead of OahuTextField's secure mode
# Android: the same options as intent extras (bwOahuThemeSwitch, bwOahuPageBg, bwOahuSafeArea, bwOahuSections, …)
```

## The upgrade

What an adopter does (`Oahu.Cli.md`: install the tool, rerun `init` with `--version`):

1. `dotnet tool update Oahu.Cli --version 0.1.0-preview.gc63d0aba --add-source ../oahu-feed/nuget`: fine.
2. `dotnet tool run oahu init --project src/App/App.csproj --feed ../oahu-feed --version 0.1.0-preview.gc63d0aba --dry-run`
   refused (below). After moving two files aside and fixing a third, it listed five updates and the restore/align step;
   the real run took 5 s ("Already aligned oahu-screens with the restored NuGet bindings: 171 Maven constraint(s)").
3. The two hand edits (icon font resource, Coil) put back by hand; the version in `src/Core/Core.csproj` and
   `src/Core.Contracts/Core.Contracts.csproj` changed by hand.
4. Built both targets: no errors, no new OAHU diagnostic on any of the 7 contracts. oahu-gen rewrote 3 generated files
   per platform (an empty keyed-list batch now publishes nothing).

### Upgrade breaks

| # | What | Exact text | Signposted? | Fix and cost |
|---|---|---|---|---|
| U1 | `init` refuses to rerun: Package.swift has the step 3 font resource (#949, G17) | `oahu: …/ios/OahuScreens/Package.swift already exists or has local changes. Move your changes aside before retrying.` | Partly. No code in text output (with `--json`: `OAHUCLI001`, `"fix": null`). errors.md's OAHUCLI001 says "The CLI could not read its inputs", which doesn't describe this case | Restore init's version from git (`b200e34d`), rerun, re-apply the 3-line edit. Same for `build.gradle.kts` (Coil, 3 lines). 5 minutes; must be repeated on every upgrade |
| U2 | `init` then refuses `gradlew.bat`, a file the app never edited (new, G37) | `oahu: …/android/gradlew.bat already exists or has local changes. Move your changes aside before retrying.` | No. The file is byte-for-byte what git holds; the repository's `.gitattributes` (`* text=auto eol=lf`) stored init's CRLF file as LF, so its SHA-256 no longer matches `setup.json`. Every fresh clone of the app is in this state | Rewrote the file with CRLF (hash then matched). 5–10 minutes to diagnose by hashing files against `setup.json` |
| U3 | `init` doesn't update the OAHU version in the two projects it doesn't manage (#931, G1) | none | No. `doctor` and `versions --check` both report the new version everywhere and say nothing about `Core.csproj` (`Oahu.Maui` 104c58c2) or `Core.Contracts.csproj` (`Oahu` 104c58c2) (G38) | Two lines by hand, found with `git grep g104c58c2` |
| U4 | `setup.json` still records init's own Package.swift and build.gradle.kts, so the next upgrade repeats U1 | — | — | Not fixable app-side |
| U5 | New analyzer rules (#986 OAHU027, #992–#1000, #1018–#1023) | none fired | — | The app's contracts already met them (`EditFieldRow.Boolean` had been renamed in step 5; no row field named `Id`, no partial or nested types, no `async void` contract methods) |
| U6 | Runtime changes to edit settlement (#1002, #1010, #1014) | no fault, no Debug check message | — | Nothing observed on either platform |

## Verdicts on the closed issues

| Issue (gap) | Verdict | Evidence |
|---|---|---|
| #935 (G5) bare CS0311 | **Fixed by documentation only** | Removing `IOahuKeyed` from `FolderRow` still gives only `error CS0311: The type 'Bit.App.Contracts.FolderRow' cannot be used as type parameter 'T' in the generic type or method 'IOahuKeyedList<T>'. There is no implicit reference conversion from 'Bit.App.Contracts.FolderRow' to 'Oahu.IOahuKeyed'.` contracts.md now names CS0311 next to the row rule, so a search finds it. Cost: nothing beyond the search |
| #939 (G9) feed paths | **Fixed by documentation** | `init` still writes paths relative to each file; `maui-migration.md` "Set up with the preview CLI" now says exactly that and how to move the feed. Rerun with the recorded feed worked |
| #942 (G12) toolbar vs composition | Not retested (closed on non-reproduction) | Nothing in passing contradicted it |
| #943 (G13) bottom safe area | **Verified fixed (by the documented recipe)** on iOS | maui.md "Whole-page safe areas": page `ios:Page.UseSafeArea="False"` + `SafeAreaEdges="None"`, the same on the `AbsoluteLayout` and the `ContentView`. With the MAUI page painted red (`BW_OAHU_PAGEBG=1`): old settings show a red strip under the list, the recipe shows none (Nord, iPhone 16 Pro). Cost: 6 lines in one page XAML, a base class that sets `UseSafeArea` must be overridden per page. Android inconclusive: the Compose folder screen draws no background of its own |
| #944 (G14) doctor output | **Verified fixed** | `doctor` now prints `warning OAHUCLI005: No Gradle resolution report under …/src/App/obj/. Run dotnet build -f net11.0-android.` with a `Try:` line first, then one summary line per item ("Restored Android NuGet bindings covered by the manifest: 38."). `versions --check` still lists every dependency, error last |
| #948 (G16) + #1021/#1022 palette | **Fixed by documentation, plus a typed value; the per-contract cost remains** | `AppPalette` now carries `OahuColor` (11 colors), read with Oahu.Maui's `ToOahuColor()`; Swift `.color`, Kotlin `.toColor()`. Replaced a hand-written ARGB `Int64` decoder on both sides (−20 lines). All **7** root contracts now carry `Palette` (folders added: 2 contract lines, 2 view-model lines, ~10 lines per screen). Nord applied on both platforms, and a live theme change (Appearance-settings path, `BW_OAHU_THEME_SWITCH`) re-themed the open folders screen and MAUI's bar on iOS and Android (screenshots before/after). Mounted sections read it from the environment (`@Entry appPalette`) with no contract member. **D26's revisit trigger is met**: every root screen of the conversion carries the palette |
| #954 (G22) sensitive values | **Fixed by documentation** | contracts.md "Sensitive values" covers what step 3 had to work out (mask in C#, native model keeps the last value, exporter keeps the last sent value, recordings hold every argument, preview fixtures write values into source). Cost unchanged: masking is contract design per screen; #959 still has no per-member exclusion |
| #956 (G24) enums with the view models | **Fixed by documentation** | contracts.md "Types declared with the view models": move the type, or mirror it with a `switch` that names both enums. The step 4 `Enum.Parse` mapping is exactly what it warns against; the app still mirrors 5 enums (not changed here) |
| #957 (G25) labelled options | **Fixed by documentation (ruling: not generic)** | contracts.md "Options with labels" shows the per-enum record the app wrote. Cost unchanged: 5 near-identical records |
| #958 (G26) every sample crosses | **Fixed by documentation** | native-screens.md now says each write is a proposal and nothing is skipped. New data: `OahuTextField` (secure mode) sends **one edit per keystroke and none on focus**, against about two per keystroke plus two on focus for SwiftUI `SecureField`/`TextField` (lock page, iOS) |
| #961 (G29) first tap | Not retested (closed on non-reproduction) | No missed first tap seen in this step on iOS (lock, add/edit, sections) |
| #975 (G32) OAHU027 | **Verified fixed** | Renamed `EditFieldRow.Toggle` back to `Boolean`: `dotnet build src/Core.Contracts` fails with `CipherAddEdit.cs(14,18): error OAHU027: Contract ICipherAddEditViewModel uses the case Bit.App.Contracts.EditFieldRow.Boolean, whose name Boolean is reserved in Kotlin, so native code could not use it there. Every platform's reserved names are refused, whatever platforms the app targets: rename it.` The same error stops `Core` for `net11.0-ios` and `net11.0-android`, before any oahu-gen run. errors.md has the section and the lists. Small point: the location is the contract's declaration line, not the case's |
| #976 (G33) focus during presentation | **Fixed by documentation only; the recipe gives no moment to wait for** | native-screens.md: "The screen holds such a request until it can act on it … Which SwiftUI and Compose moment is the earliest that a presented field accepts focus has not been checked." With the page's 400 ms delay removed (`BW_OAHU_FOCUS=now`) the name field **did** take focus in 4 of 4 runs here (step 5 saw it fail), so the original failure didn't reproduce. A held request retried every 100 ms (`BW_OAHU_FOCUS=held`) kept focus on attempt 4 (~300 ms after delivery), so focus does not stick during part of the presentation. The app keeps MAUI's delay |
| #977 (G34) | Closed not planned; not retested | Step 6 found it was not OAHU |

## Open issues: did anything change?

- **#931 (G1)**: unchanged, and worse on upgrade: the versions in projects `init` doesn't manage go stale silently (U3, G38).
- **#933 (G3)**: this upgrade aligned (171 constraints) and the first Android build had no OAHUVER error; the app
  already pins Jdk7/Jdk8, so this is not a retest of the original case.
- **#938 (G8)**: unchanged (`com.oahu.appapp`).
- **#949 (G17)**: unchanged, and a second cause found (U2, G37).
- **#960 (G28)**: **largely resolved on iOS** by #982. `OahuTextField(…, isSecure:)` on the lock page and the add/edit
  secrets: the eye keeps the same field, focus, keyboard and cursor (cursor moved left 3, hidden, typed `Y`, shown:
  `abcYdeX` with the caret after `Y`); typing while hidden didn't clear; a hidden custom field row kept focus and
  appended (`0000` → `00007`). SwiftUI `.focused($state)` on an `OahuTextField` worked (focus on appearing from the
  `FocusSecret` event, refocus after the wrong-password alert), although native-screens.md says to use `TextField` for
  SwiftUI focus management. Still missing: placing the cursor from the view model (`SecretFocus.Cursor` stays ignored),
  and iOS clears a secure field on the first keystroke after it regains focus (documented, as MAUI's `Entry`).
- **#973 (G30)**: **D25 works under `OahuView` on both platforms** (below).
- **#974 (G31)**: sidestepped by D25: the section's fields are 17 string editables, so Compose uses
  `rememberTextFieldState()` and a keystroke sends one string.
- **#978 (G35)**: closed by #1036 after this preview was packed; not tried.
- Not touched: #932, #934, #936, #937, #940, #941, #947, #950–#953, #955, #959, #979.

## D25 trial: sections as mounted child contracts under MAUI's `OahuView`

Built: `IIdentitySectionViewModel` (title picker + 17 string editables + a focus event) and `IUriSectionViewModel` (the
editable URI keyed list, add/set text/options, a row focus event) in `src/Core.Contracts/CipherAddEditSections.cs`;
their view models nested in `CipherAddEditPageViewModel.Sections.cs` over the same `CipherView` and `Uris` collection;
`IdentitySection.oahu-screen.swift/.kt` and `UriSection.oahu-screen.swift/.kt`. The parent contract gained
`IOahuState<CipherAddEditSections> Sections` (two mount ids, 0 = draw the flat members) and `void MoveFocus(FormFocus)`.
The page mounts the sections after load when `BW_OAHU_SECTIONS=1` (`Oahu.iOS.OahuScreens.Mount<T>` /
`Oahu.Android.OahuScreens.Mount<T>`, under `#if IOS` / `#elif ANDROID`) and disposes them on `OahuView.TornDown`. Without
the switch the step 5 flat form draws as before.

Result, stated plainly: **it works under `OahuView`, on iOS and Android, with no OAHU error and no workaround.**

- **Drawing**: SwiftUI `OahuMountView(id:)` inside the parent `Form` (`Section { … }` for URIs, inline for identity):
  the section's rows became rows of the parent's Form, indistinguishable from the flat form. Compose `OahuMount(id)` in
  the parent's scrolling `Column`: same. One scroll, one form.
- **Environment**: the sections inherited the parent's `colorScheme`, `tint` and an `appPalette` environment key
  (iOS, Nord verified) and the parent's `MaterialTheme` (Android). No palette member on the section contracts.
- **Focus and keyboard**: within a section, `@FocusState`/Next works. **Into** a section: Android's default Next
  traversal crossed from the parent's Name field into the section (it landed on the Title dropdown). iOS: the parent
  can't reach the section's `@FocusState`, so Return on Name calls the new parent method `MoveFocus`, C# forwards it to
  the section's focus event, and the section focuses First name (verified, `MoveFocus` then `FirstName` edits in the
  diagnostics). A parent focus request for a section field (Save's validation, a new URI) is forwarded the same way.
  Leaving a section the other way was not needed here (the identity's last field is "done").
- **Per-keystroke payload**: one string. iOS: 6 `IdentitySection.FirstName` edits for 3 characters (SwiftUI's two per
  keystroke, each one string, was 17 strings); Android: one per keystroke. Save stored the typed value
  (`identity.first='Aliceabc'` iOS, `'Alicexyz'` Android).
- **Open cost**: mounting both sections took 36–44 ms on iOS (identity 18–26 ms, URIs 13–15 ms) and 66–251 ms on
  Android (identity 50–157 ms) on this loaded host; they are mounted before the parent screen opens, so there was no
  visible pop-in. Parent `ScreenOpenFinished` 34–46 ms (iOS) and 60–424 ms (Android), as before.
- **Teardown**: popping the page closed the parent screen first, then each section (`ScreenCloseFinished` 1–2 ms), then
  ran the mount callbacks; no fault, both platforms.
- **Rows**: URI section add (focus moved to the new row), type, options sheet, remove: all on iOS; typing in a row on
  Android.
- **Android state**: a fold and unfold of the emulator with the sectioned form open kept the typed first name and both
  sections (MAUI handles the configuration change; the process survived). Process death was not tried (step 4: the debug
  route restarts from scratch). Each section's `ComposeView` is inside the parent's composition, so the parent's saved
  state view ID covers it (inferred).
- **What it cost**: contracts +83 (+12 on the parent), view models +206 (+7), page +24, Swift +131 (+~30 in the parent),
  Kotlin +165 (+~13 in the parent); generated code +1,950 lines for the two section contracts (IdentitySection.oahu.swift
  alone is 1,713 lines: about 90 per string editable). In a real split the flat identity (C# `IdentityFields`, its
  read/write, ~60 lines of Swift and Kotlin) and URI members would be deleted; here they stay for comparison.
- **Friction found**: the parent needs a contract method only to move native focus into a section on iOS (G40); the
  view model needs the platform's `OahuScreens.Mount` from page code under `#if` and the page must remember to dispose
  the mounts on `TornDown` (G39).

## Size of this step

| | Lines |
|---|---|
| Hand-written (C#, XAML, Swift, Kotlin, excluding generated) | +908 / −114 across 34 files |
| of which D25 (contracts, view models, page, four section screens, parent edits) | about +700 |
| Generated native code | +3,748 / −223 across 28 files |

## Not done or not verified

- `OAHU_Pixel_API36` (blocked; used `OAHU_Fold_API36` read-only). Android lock page, vault list and item view were not
  opened (Groupings and Generator opened in the background without fault).
- Android safe area for #943 (inconclusive); #978's new doc (after this preview).
- A theme change on the add/edit form itself; Nord on Android sections (Material theme inherited, not screenshotted).
- Process death with sections; activity recreation without a configuration change.
- #942 and #961 were not re-run; #976's failure did not reproduce, so the held-request recipe was not validated against
  a failing case.
