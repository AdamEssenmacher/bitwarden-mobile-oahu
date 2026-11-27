# Step 8: interactions that cross a section boundary

Branch `oahu/08-cross-section`, cut from `oahu/07-retest` (535b64db). Same OAHU preview, `0.1.0-preview.gc63d0aba`.

Question: step 7 split the add/edit form into sections (D25: child contracts drawn with `OahuMountView` /
`OahuMount`) and found that on iOS the parent could move focus into a section only through C# (G40,
[AdamE.Oahu#1049](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/1049)). Is that a one-off or a pattern? This
step built three more cross-section interactions on the sectioned identity form. For each one it tried a native-only
version first, on iOS and Android, and built the C# route where it was needed or worth comparing.

Effort: about 1 hour 10 minutes of wall-clock time (35 minutes building and running, the rest writing up). Host load average 12–76 from other sessions during the runs, so
timings are indicative only. iOS: "BW OAHU Repro977 HID" (iPhone 16 Pro, iOS 18.6), `iossimulator-x64` Debug. Android:
`OAHU_Fold_API36`, read-only, port 5712 (another session held `OAHU_Pixel_API36` without `-read-only`).

## Verdict

**#1049 is not a pattern. On both platforms, every view-only interaction tried here works natively across the mount
boundary, with no contract member and no C#.** G40 was a missing recipe, not a missing channel. `OahuMountView` draws
the section as an ordinary SwiftUI view inside the parent's view tree (no hosting controller in between), and
`OahuMount` calls the section's composable inside the parent's composition. So SwiftUI focus, environment,
preferences, focused values and scroll ids are shared with the parent, and so are Compose's focus tree, composition
locals and bring-into-view.

Two qualifications:

- **The recipe is native plumbing the app writes once** (`CrossSection.swift`, 83 lines; `CrossSection.kt`, 60 lines,
  both mostly general), plus per-screen wiring. Nothing in OAHU's docs says these channels cross a mount, which is why
  step 7 went through C#. The doc gap is the finding (G44).
- **The C# route is worse than it looked in step 7, on iOS.** Forwarding a focus request to the section's own focus
  event (the G40 workaround) does nothing when the field is off screen in the parent's `Form`: the section can set only
  its own `@FocusState`, the row isn't built yet, and the section can't scroll the parent's list (G45). The native
  route (the parent scrolls to an id inside the section, then sets the shared focus) works.

Nothing here argues against D25 itself. Interactions that cross sections are not forced into the view model; they are
forced into the native screen package, where they belong, as long as the app knows to put them there.

## What was built

All of it is behind DEBUG switches; without them the app behaves as in step 7.

| Switch | iOS | Android | Effect |
|---|---|---|---|
| `BW_OAHU_REQEMAIL=1` | env var (`SIMCTL_CHILD_…`) | extra `bwOahuReqEmail` | DEBUG-only validation rule: an identity with an empty Email fails Save with "The Email field is required." and asks for focus on `FormField.IdentityEmail` |
| `BW_OAHU_CROSS=native` | env var | extra `bwOahuCross` | C# forwards no focus request into a section (`ForwardFocusToSection` returns false); the screens route it natively, and the parent draws a Previous/Next bar and an "editing <section>" header |
| unset (with `BW_OAHU_SECTIONS=1`) | | | step 7's routes: Return on Name calls `MoveFocus`, C# forwards focus requests to the section's `Focus` event (now also Email, line 8) |

Contract: one enum case, `FormField.IdentityEmail` (the field the validation rule names). No new member.

```sh
# iOS
SIMCTL_CHILD_BW_OAHU_DEBUG=edit SIMCTL_CHILD_BW_OAHU_EDIT=edit:identity-alice SIMCTL_CHILD_BW_OAHU_SECTIONS=1 \
  SIMCTL_CHILD_BW_OAHU_REQEMAIL=1 SIMCTL_CHILD_BW_OAHU_CROSS=native \
  xcrun simctl launch --console-pty <udid> com.8bit.bitwarden          # drop _CROSS for the via-C# route
# Android
adb -s emulator-5712 shell am start -n com.x8bit.bitwarden/com.x8bit.bitwarden.MainActivity \
  --es bwOahuDebug edit --es bwOahuEdit edit:identity-alice --es bwOahuSections 1 \
  --es bwOahuReqEmail 1 --es bwOahuCross native
```

In native mode the iOS screens print `OAHUDEBUG cross …` lines: the shared focus on every change, whether a focus
request was kept and on which attempt, the order collected by the preference, and the focused-value probe.

### iOS recipe (`CrossSection.swift`, `CipherAddEdit.oahu-screen.swift`, `IdentitySection.oahu-screen.swift`)

- **One focus for the whole form.** The parent owns `@FocusState var shared: String?` and puts `$shared` in the
  environment (`@Entry var formFocus: FocusState<String?>.Binding?`). Each field, in the parent or a section, applies
  `.formField("identity.IdentityEmailEntry", local: $focus, equals: 8)`: bound to the shared focus when the environment
  has one, otherwise to the view's own `@FocusState` (step 7 behaviour).
- **Order.** Each screen declares its full field list into a plain `FormOrder` object from the environment: the section
  under its name, the parent as a template naming where each section goes (`["parent.name", "@identity",
  "parent.notes"]`). Return (via an environment closure `formAdvance`) and the bar's Previous/Next move through
  `formOrder.all`.
- **Scroll and focus.** The parent wraps its `Form` in a `ScrollViewReader`. Each section row carries `.id(fieldID)`.
  To focus a field the parent calls `proxy.scrollTo(id, anchor: .center)`, sets `shared = id`, and re-sets it every
  50 ms until it sticks (at most 10 tries; a newer move cancels an older one).
- **Bar.** A `.safeAreaInset(edge: .bottom)` HStack with Previous/Next while `shared != nil`, not a keyboard toolbar
  (G46).
- **Up.** The parent's section header reads `shared` ("ITEM INFORMATION · EDITING IDENTITY").

### Android recipe (`CrossSection.kt`, `CipherAddEdit.oahu-screen.kt`, `IdentitySection.oahu-screen.kt`)

- **Targeted focus.** A `FormFocusRegistry` (id → `FocusRequester`) in a `CompositionLocal`; `Modifier.formField(id)`
  registers a field. The parent handles `FormField.IdentityEmail` with `registry.requestFocus("identity.IdentityEmailEntry")`.
- **Traversal.** Nothing app-specific: the bar calls `LocalFocusManager.current.moveFocus(FocusDirection.Next /
  Previous)`; the section's fields are in the same focus tree, in layout order.
- **Up.** `Box(Modifier.onFocusChanged { identityHasFocus = it.hasFocus }) { OahuMount(sections.identity) }`: no help
  from the section at all.
- The section now keeps one `FocusRequester` per line (it had one for the first line), so the via-C# route can focus
  Email too.

## Results

All rows below were run on the simulator or emulator unless marked inferred.

| Interaction | Platform | Native only | Via C# |
|---|---|---|---|
| 1. Save with an empty Email (identity section, off screen, focus in the parent's Notes far below): scroll to and focus Email | iOS | **Works with caveats.** Parent maps `IdentityEmail` to the section's field id, `scrollTo`, shared focus. The list scrolled up and Email took focus with the keyboard; the shared focus was kept on attempt 5 (~200 ms), so the retry is needed for a row that isn't built yet. | **Fails.** C# forwards to the section's event (line 8); the section sets its own `@FocusState`; nothing scrolls and focus stays in Notes (screenshot-verified). Fixing it needs the parent's `ScrollViewProxy` in the section, which is the native plumbing anyway. |
| | Android | **Works.** Registry `requestFocus`; the non-lazy scrolling `Column` brought Email into view (it ended at the top edge of the viewport, under the app bar's lower edge). | **Works.** C# forwards to the section's event; the section's own requester focuses Email and Compose brings it into view. |
| 2. Previous/Next across the parent's fields and the section, both directions, leaving the section | iOS | **Works with caveats.** Name → (Next ×17) → First name … Country → Notes (leaves the section); Previous Notes → Country (enters from below); Previous First name → Name (leaves upward); Return on Name → First name and Return on Country → Notes, both without C#. Caveats: a SwiftUI preference crosses the mount but in a `Form` it carries only built rows, in build order (scrolling up gave `parent.notes Country PostalCode State …`), so each screen must declare its order (FormOrder); moving to an unbuilt row briefly drops focus to nil (seen Phone → Address 1, kept on attempt 3); the keyboard toolbar placement was unreliable (G46). | Not built: would need the parent and C# to know every section's field order (view-only knowledge in the view model). |
| | Android | **Works.** `moveFocus` Next: Email → Phone, Country → Folder (leaves the section); Previous: Folder → Country, First name → Title → Name. IME Next already crossed in step 7. Caveat: the bar sits behind the keyboard because `imePadding()` has no effect under `OahuView` (G46); tested with the keyboard dismissed, after which each move reopened it. | Not needed. |
| 3. Upward, view-only: the parent shows which section has focus | iOS | **Works.** Header reads the shared focus: "· EDITING PARENT" / "· EDITING IDENTITY". Also verified: a `focusedValue` set by the section's field is read by the parent's `@FocusedValue` (`OAHUDEBUG cross focusedValue section identity`), with no shared binding. | Not needed. |
| | Android | **Works.** `onFocusChanged` on a `Box` around `OahuMount`: "· EDITING IDENTITY" while a section field has focus, plain header after Previous back to Name. | Not needed. |
| 3b. Save disabled while a section field is mid-edit | both | **Impossible natively**, but not because of sections: Save is the MAUI page's toolbar item, and a hosted screen can't reach MAUI's chrome (maui.md "Known limits"). The same is true of a field in the parent. | Would need a contract state; not built. |

### What crossed the boundary, per channel

| Channel | Crosses the mount? | Evidence |
|---|---|---|
| SwiftUI environment value | Yes | step 7 (palette); here `formFocus`, `formAdvance`, `formOrder` (verified) |
| `FocusState.Binding` through the environment | Yes | section fields bound to the parent's `@FocusState`; parent observes every change (verified) |
| Preference key, section → parent | Yes, but in a `Form` only built rows, in build order | `OAHUDEBUG cross order` lines (verified) |
| `focusedValue` / `@FocusedValue` | Yes | verified |
| `ScrollViewReader.scrollTo` with an id inside the section | Yes | Email row, off screen, scrolled into view (verified) |
| `focusedSceneValue`, anchor preferences, `.focusScope` / `prefersDefaultFocus` | Not tried | `prefersDefaultFocus` is tvOS/macOS-oriented; anchor preferences go through the same preference path |
| `ToolbarItemGroup(placement: .keyboard)` | Not a section question; unreliable under `OahuView` | G46 |
| Compose `CompositionLocal` | Yes | step 7 (theme); `LocalFormFocus` here (verified) |
| Compose `FocusRequester` created in the parent's registry, attached in the section | Yes | verified |
| `FocusManager.moveFocus` / default traversal | Yes | verified (and step 7's IME Next) |
| `onFocusChanged` around `OahuMount` | Yes (`hasFocus` sees the section's fields) | verified |
| Bring into view on focus | Yes | verified |
| `imePadding()` | Not a section question; no effect under `OahuView` in this MAUI page | G46 |

Why it crosses: `OahuMountView.body` is `CapturedMount(id:).id(id)`, whose body returns the registered screen's
`AnyView` directly (`OahuRuntime/Mounts.swift`): no `UIHostingController`, no separate environment root.
`OahuMount(id)` looks up the registered `@Composable () -> Unit` and calls it inside `key(id)` in the caller's
composition (`oahu-compose/OahuMount.kt`). No runtime change is needed for any channel above.

## What the view model holds

- **Native route:** nothing view-only. C# says which field is invalid (`FormField.IdentityEmail`); the screens decide
  where it is, how to scroll to it, and the order of fields.
- **Via C# (step 7 and the comparison here):** `ForwardFocusToSection` knows which section draws Email and its line
  index in that section's UI (`RequestFocus(8)`); step 7's `MoveFocus` contract method exists only to relay Return on
  Name. Both are view-only knowledge in the view model, and on iOS the first doesn't even work off screen.

## Size

Hand-written lines added (excluding generated code), `git diff --numstat` against 535b64db:

| | Native (Swift / Kotlin) | C# |
|---|---|---|
| General recipe, any section | `CrossSection.swift` 83, `CrossSection.kt` 60 | — |
| Parent wiring (ids, request mapping, scroll/focus helper, bar, header) | Swift +100/−4, Kotlin +44/−4 | — |
| Section wiring | Swift +15/−3, Kotlin +5/−3 | — |
| DEBUG validation rule (test fixture, both routes) | — | +13 |
| Contract enum case | — | +2/−1 |
| Via-C# route for #1 (forward Email; native-mode switch) | Kotlin: one requester per line (in the section's +5) | +14 |

Roughly a third of the native lines are comments. Per screen, the app writes the field ids, the parent's order
template and the mapping from the contract's field enum to ids; the rest is reusable.

## Not done or not verified

- The URI section (keyed rows): not exercised across the boundary here (step 7 covered add-and-focus inside it).
- The C# route for traversal (#2) was not built; it was judged from what it would have to carry.
- `focusedSceneValue`, anchor preferences, `.focusScope`: not tried.
- iOS hardware-keyboard Tab traversal; VoiceOver/TalkBack order across the boundary.
- Why the iOS keyboard toolbar shows only sometimes, and whether Android's `imePadding()` would work with a different
  MAUI soft-input mode (G46): causes inferred, not investigated.
- A `LazyColumn` parent on Android (non-lazy `Column` here; a lazy parent would need `scrollToItem` before
  `requestFocus`, like iOS) and a `ScrollView`/`VStack` parent on iOS (no lazy rows; inferred simpler).
- Devices, Release builds.
