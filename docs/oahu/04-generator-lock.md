# Step 4: the generator and the lock page as native screens

Branch `oahu/04-generator-lock`, cut from `oahu/03-vault` (05eadfed). Goal: move the generator (`GeneratorPage`, the
Generator tab and the modal add/edit opens to pick a value) and the lock page (`LockPage`) to SwiftUI and Compose
screens hosted in `OahuView`, keeping MAUI's tabs, navigation, titles, toolbar items and dialogs, and probe editable
non-string values, policy clamping, secure text, focus, one-shot signals, G12 and G20. Findings are in
[gaps.md](gaps.md) (G24–G29, updates under G1, G12, G20, G22); this file is what was done.

OAHU: the same preview, `0.1.0-preview.g104c58c2`, from `~/RiderProjects/oahu-feed`.

Effort: about 1 hour 15 minutes of wall-clock time: 15 minutes reading the pages and docs, 20 minutes contracts,
view-model halves and the four screens (Swift and Kotlin compiled first time), 15 minutes DEBUG routes and fakes
(three debug-harness fixes after the first lock run), 20 minutes on the simulator and emulator, 10 minutes writing up.
Host load average 7–38 from other sessions during the runs.

## Results

| Target | Build | Run |
|---|---|---|
| `net11.0-ios`, `iossimulator-x64`, Debug | Yes | iOS 18.6 simulator "BW OAHU iPhone": generator tab (regenerate, slider drag with live regeneration, toggles, passphrase, word separator normalized to one character), policy route (banner, locked toggles, slider snapping back to 20 on release, stepper clamped at 3, also with slow save and live clamp), username modal (plus-addressed with website, forwarded alias with service picker, API key shown/hidden, refused key → MAUI alert, generated alias, Select returned it and closed the modal); lock with master password (biometric auto-prompt, fallback focuses the field, show/hide, Go, wrong password alert, unlock to the vault, relock on resume), PIN (autofocus, number pad, unlock; then an app crash, see gaps "Not OAHU") |
| `net11.0-android`, Debug | Yes | Emulator `OAHU_Pixel_API36` (read-only, port 5580): generator tab (slider round trips counted), policy route with live clamp (thumb held at 20), username modal and Select, tab switch and back; lock with biometric prompt, fallback focus, show/hide keeping focus, Go, wrong password alert, unlock, relock on resume with the auto-prompt, unlock by the fake biometric; PIN with autofocus; G12 attempt on the step 2 folder page with Gboard; G20 process-death check with the vault drill-down |

Build and run:

```sh
cd src/App
dotnet build -f net11.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-x64
xcrun simctl install <udid> bin/Debug/net11.0-ios/iossimulator-x64/App.app
SIMCTL_CHILD_BW_OAHU_DEBUG=generator-policy xcrun simctl launch <udid> com.8bit.bitwarden
#   generator | generator-policy | lock | lock-pin | lock-bio | lock-pin-bio | lock-bio-only
#   SIMCTL_CHILD_BW_OAHU_GEN=modal|modal-username, _SLOWSAVE=1, _CLAMP_LIVE=1, _BIO=real|invalid, _DIAG=1

dotnet build -f net11.0-android -c Debug -t:Install -p:AdbTarget="-s emulator-5580"
adb shell am start -n com.x8bit.bitwarden/com.x8bit.bitwarden.MainActivity --es bwOahuDebug lock-bio --es bwOahuDiag 1
```

Debug master password `debug`, PIN `1234`; API key `bad` is refused by the fake forwarding service.

## What changed

### Contracts (`src/Core.Contracts`)
- `Generator.cs`: `IGeneratorViewModel` — `Copy` (labels and the localized option lists), `Palette`, `Layout`
  (sections, banner, labels that follow the choices), `Locks` (policy-fixed options), `Generated` (the value as
  `TextRun`s), 25 editables (kind, password type, length, character sets, minimums, words, separator, username type,
  emails, email types, service, API key, domains, random-word options), `IOahuEvents<Unit> Copied`, and methods
  `CopyValue`, `Regenerate`, `LengthEditingEnded`, `ToggleApiSecret`, `ShowUsernameTypeHelp`. Five mirror enums and
  five option records (G24, G25).
- `Lock.cs`: `ILockViewModel` — `Copy`, `Palette`, `Layout`, editables `MasterPassword` and `Pin`,
  `IOahuEvents<SecretFocus> FocusSecret`, methods `Submit`, `ToggleShowSecret`, `UseBiometrics`.

### View models (existing classes, now `partial`)
- `GeneratorPageViewModel.Oahu.cs`: every editable is `OahuEditable.FromProperty` over the property the XAML bound,
  so the existing setters (save, normalize against the policy, reload, regenerate) decide each answer. The commands
  keep their error dialogs. DEBUG counters log length edits.
- `LockPageViewModel.Oahu.cs`: editables over `MasterPassword`/`Pin`; `FocusSecretEntry` (weak event, can arrive off
  the main thread) is forwarded to `FocusSecret`; `RequestSecretFocus()` replaces the page's `RequestFocus`.
- `src/Core/Utilities/OahuText.cs`: the letters/digits/symbols runs, shared with the item view (moved from
  `CipherDetailsPageViewModel.Oahu.cs`).
- `src/Core/Utilities/OahuAndroidIds.cs` and `src/App/Platforms/Android/Resources/values/ids.xml`: saved-state view
  IDs for the generator tab, generator modal and lock page (G1, G20).

### Pages
- `GeneratorPage.xaml`: the 500-line form is one `OahuView<IGeneratorViewModel>`; toolbar items (Cancel/Select,
  history, options) unchanged. Code-behind: picker update modes, the HTML label's visibility toggling and the slider's
  `DragCompleted` are gone (the screen calls `LengthEditingEnded`).
- `LockPage.xaml`: the `ScrollView` form is an `OahuView<ILockViewModel>`, still swapped into `_mainContent` on first
  appearance. Toolbar, account avatar and switcher overlay unchanged. Code-behind: `SecretEntry`, the focus handler and
  the Unlock/biometric click handlers are gone.

### Native screens (`src/App/.oahu/App/`)
- SwiftUI: `Generator.oahu-screen.swift` (`Form`, menu `Picker`s, a continuous `Slider` over the Int, `Stepper`s,
  `Toggle`s disabled by policy, `SecureField`/`TextField` API key with eye, coloured value with copy/regenerate and a
  haptic on `Copied`), `Lock.oahu-screen.swift` (`SecureField`/`TextField` swap, `@FocusState` driven by
  `FocusSecret`, Go submits, biometric and Unlock buttons).
- Compose: `Generator.oahu-screen.kt` (scrolling column, `ExposedDropdownMenuBox` pickers, `Slider` with
  `onValueChangeFinished`, minus/plus steppers, `OutlinedSecureTextField` for the API key), `Lock.oahu-screen.kt`
  (one `OutlinedSecureTextField` whose obfuscation follows the eye, `FocusRequester` and cursor from `FocusSecret`,
  `commit()` before submit).
- Glyphs `generate` and `questionCircle` added to the shared icon tables; generated files committed beside them.

### Debug routes (DEBUG only, `src/Core/Pages/Settings/OahuDebug/`)
- `OahuDebugRoute.cs`: `generator`, `generator-policy` (opens the Generator tab, optionally the modal) and the five
  lock routes; relock on resume; optional diagnostics sink (metadata only).
- `OahuDebugLock.cs`: lock state, a crypto decorator that accepts `debug` and the PIN `1234` (every other member
  forwards to the real service; `OahuDebugCryptoService.Generated.cs` by `gen_decorator.py`), a platform-utilities
  decorator with a fake biometric prompt, fake biometric, user-verification and device-trust services, and a username
  generator that never calls a forwarding service.
- `OahuDebugStateService.Step4.cs`: in-memory generator options (optionally saved after a yield), a fake access token
  for the plus-addressed email, PIN placeholders, unlock attempts, previous page. The state decorator was regenerated.
- `FakeVaultServices.cs`: the fake policy can carry a generator policy; the fake vault timeout locks and navigates to
  LockPage.

## Left in MAUI
- Every dialog and sheet (errors, invalid password, logout confirmation, options menu, the loading overlay), the
  toolbar items, the account avatar and the account switching overlay (not opened), the password history page.

## Not done or not verified
- iOS marked-text (Japanese) composition for G12; the real Face ID / fingerprint prompt (`BW_OAHU_BIO=real` is built,
  not run); biometric-only and invalidated-biometric layouts (built, not run); Android activity-only recreation for G20.
- Theme switching while the screens are open; Nord on these screens (they take the shared palette, not exercised).
- Copy (the clipboard and toast run in C#; the haptic was not observable on the simulator/emulator), the username-type
  help link, catch-all and random-word options were not all tapped on both platforms (catch-all and random word not
  at all).
- Real accounts, Release builds, devices, TalkBack/VoiceOver, Dynamic Type.

## Size

| | Lines |
|---|---|
| App C#/XAML changed in existing files (two pages, two view models, item-view helper) | +31 / -722 |
| New C#: contracts (`Generator.cs`, `Lock.cs`) | 217 |
| New C#: view-model halves, `OahuText`, `OahuAndroidIds` | 365 |
| New C#: DEBUG routes and fakes (62 of them generated forwarders) | 383 new, +224/-29 in existing files |
| Native code written by hand: Swift 427, Kotlin 439 | 866 |
| Generated native code (committed) | 44 files |

## Security-relevant notes
- No crypto, key derivation, secure storage, keychain or biometric code changed. The lock routes replace the crypto,
  biometric, user-verification and device-trust services for the debug process only, and must never run on a device
  with a real account; they are `#if DEBUG` and need the launch variable or extra.
- The master password, PIN and forwarding-service API keys cross to C# on every keystroke, as the MAUI bindings did
  (G26). OAHU's diagnostics carry no values (verified); a session recording would carry them all (G27), so don't record
  on these pages. The Compose text state is not saved to the instance bundle; nothing is persisted natively.
- The generated password is in the native model while shown, as it was in MAUI's label; copying runs in C#.
